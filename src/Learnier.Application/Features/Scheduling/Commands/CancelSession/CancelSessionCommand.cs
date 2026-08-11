using FluentValidation;
using Learnier.Application.Common.Abstractions;
using Learnier.Application.Common.Results;
using Learnier.Domain.Scheduling;

namespace Learnier.Application.Features.Scheduling.Commands.CancelSession;

public sealed record CancelSessionCommand(
    Guid SessionId,
    string? Reason = null,
    bool IsInstructorInitiated = false);

public sealed record CancelSessionResult(int CancelledBookingCount);

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
    ICurrentTenant currentTenant,
    IUnitOfWork unitOfWork,
    IClock clock)
{
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

            if (clock.UtcNow >= session.StartsAt.AddHours(-1))
            {
                return SchedulingErrors.InstructorCancellationDeadlinePassed;
            }
        }

        if (session.Status is LessonSessionStatus.Completed)
        {
            return SchedulingErrors.SessionNotCancellable;
        }

        if (session.Status is LessonSessionStatus.Cancelled)
        {
            return new CancelSessionResult(0);
        }

        var now = clock.UtcNow;
        var bookings = await scheduling.ListActiveBookingsAsync(session.Id, cancellationToken);

        foreach (var booking in bookings)
        {
            booking.Cancel(now, command.Reason);
            await entitlements.ReleaseAsync(booking, refundable: true, cancellationToken);
        }

        session.Cancel(command.Reason);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new CancelSessionResult(bookings.Count);
    }
}
