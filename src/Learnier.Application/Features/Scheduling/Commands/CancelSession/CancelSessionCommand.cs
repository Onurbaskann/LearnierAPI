using FluentValidation;
using Learnier.Application.Common.Abstractions;
using Learnier.Application.Common.Results;
using Learnier.Domain.Scheduling;

namespace Learnier.Application.Features.Scheduling.Commands.CancelSession;

public sealed record CancelSessionCommand(
    Guid SessionId,
    string? Reason = null,
    bool IsInstructorInitiated = false);

/// <param name="StudentCreditsRefunded">Gercekten iade edilen ders hakki sayisi.</param>
/// <param name="PenaltyApplied">Egitmene bu iptal icin kesinti kaydedildi mi.</param>
/// <param name="PenaltyPercentage">Kesinti uygulandiysa orani, aksi halde sifir.</param>
public sealed record CancelSessionResult(
    int CancelledBookingCount,
    int StudentCreditsRefunded,
    bool PenaltyApplied,
    decimal PenaltyPercentage);

internal sealed class CancelSessionValidator : AbstractValidator<CancelSessionCommand>
{
    public CancelSessionValidator()
    {
        RuleFor(c => c.Reason)
            .MaximumLength(500).WithErrorCode("scheduling.cancellation_reason_too_long");
    }
}

/// <summary>Oturumu iptal eder ve tum aktif rezervasyon haklarini iade eder.</summary>
public sealed class CancelSessionHandler(
    ISchedulingRepository scheduling,
    IInstructorRepository instructors,
    IBookingEntitlementPolicy entitlements,
    IInstructorCompensationService compensation,
    ICurrentTenant currentTenant,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    /// <summary>Politika snapshot'i olmayan eski oturumlar icin cezasiz iptal siniri.</summary>
    private const int DefaultInstructorCutoffHours = 4;

    public async Task<Result<CancelSessionResult>> Handle(
        CancelSessionCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!currentTenant.HasTenant)
        {
            return SchedulingErrors.OrganizationContextRequired;
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        var session = await scheduling.FindSessionForUpdateAsync(
            command.SessionId, cancellationToken);

        if (session is null)
        {
            return SchedulingErrors.SessionNotFound;
        }

        Guid? cancellingInstructorProfileId = null;
        if (command.IsInstructorInitiated)
        {
            if (currentTenant.MembershipId is not { } membershipId)
            {
                return SchedulingErrors.OrganizationContextRequired;
            }

            var profile = await instructors.FindByMembershipAsync(membershipId, cancellationToken);
            if (profile is null)
            {
                return SchedulingErrors.InstructorNotFound;
            }

            session = await scheduling.FindSessionAsync(command.SessionId, true, cancellationToken);
            if (session is null)
            {
                return SchedulingErrors.SessionNotFound;
            }

            if (session.Instructors.All(item => item.InstructorProfileId != profile.Id))
            {
                return SchedulingErrors.SessionNotOwned;
            }

            if (!await scheduling.LockInstructorAsync(profile.Id, cancellationToken))
            {
                return SchedulingErrors.InstructorNotFound;
            }

            if (clock.UtcNow >= session.StartsAt)
            {
                return SchedulingErrors.InstructorCancellationDeadlinePassed;
            }

            cancellingInstructorProfileId = profile.Id;
        }

        if (session.Status is LessonSessionStatus.Completed)
        {
            return SchedulingErrors.SessionNotCancellable;
        }

        if (session.Status is LessonSessionStatus.Cancelled)
        {
            return new CancelSessionResult(0, 0, false, 0m);
        }

        var now = clock.UtcNow;
        var bookings = await scheduling.ListActiveBookingsAsync(session.Id, cancellationToken);

        // Yeni oturumlarda son tarih kurum politikasından snapshot olarak gelir.
        // Eski kayıtlarda geriye uyumluluk için dört saatlik varsayılan uygulanır.
        var instructorDeadline = session.InstructorCancellationDeadlineAt
            ?? session.StartsAt.AddHours(-DefaultInstructorCutoffHours);

        // Boş slotun kapatılması iptal sayılmaz: ceza yalnızca gerçekten bir
        // öğrenciyi etkileyen geç iptalde doğar ve ders başına tek olaydır.
        var penaltyApplied = false;
        var penaltyPercentage = 0m;
        if (cancellingInstructorProfileId is { } instructorProfileId
            && bookings.Count > 0
            && now > instructorDeadline)
        {
            var penalty = await compensation.RegisterLateCancellationAsync(
                instructorProfileId,
                session.Id,
                cancellationToken);
            if (penalty.IsFailure)
            {
                return penalty.Error;
            }

            penaltyApplied = penalty.Value.PenaltyApplied;
            penaltyPercentage = penalty.Value.Percentage;
        }

        var refundedCount = 0;

        foreach (var booking in bookings)
        {
            booking.Cancel(now, command.Reason);
            var release = await entitlements.ReleaseAsync(
                booking,
                refundable: true,
                cancellationToken);
            if (release.IsFailure)
            {
                return release.Error;
            }

            if (release.Value)
            {
                refundedCount++;
            }
        }

        session.Cancel(command.Reason);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new CancelSessionResult(
            bookings.Count,
            refundedCount,
            penaltyApplied,
            penaltyPercentage);
    }
}
