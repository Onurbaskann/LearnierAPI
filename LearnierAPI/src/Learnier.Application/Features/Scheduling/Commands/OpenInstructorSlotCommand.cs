using FluentValidation;
using Learnier.Application.Common.Abstractions;
using Learnier.Application.Common.Results;
using Learnier.Domain.Catalog;
using Learnier.Domain.Scheduling;
using Learnier.Domain.Teaching;

namespace Learnier.Application.Features.Scheduling.Commands.OpenInstructorSlot;

public sealed record OpenInstructorSlotCommand(
    Guid CourseId,
    DateTimeOffset StartsAt);

public sealed record OpenInstructorSlotResult(
    Guid SessionId,
    Guid CourseId,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    int LessonDurationMinutes);

internal sealed class OpenInstructorSlotValidator : AbstractValidator<OpenInstructorSlotCommand>
{
    public OpenInstructorSlotValidator()
    {
        RuleFor(command => command.CourseId)
            .NotEmpty().WithErrorCode("scheduling.course_required");

    }
}

/// <summary>Egitmenin kendi takviminde tek bir birebir ders slotu acar.</summary>
public sealed class OpenInstructorSlotHandler(
    ISchedulingRepository scheduling,
    IInstructorRepository instructors,
    ICatalogRepository catalog,
    ICancellationPolicyService cancellationPolicies,
    ICurrentTenant currentTenant,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    public async Task<Result<OpenInstructorSlotResult>> Handle(
        OpenInstructorSlotCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (currentTenant.OrganizationId is not { } organizationId
            || currentTenant.MembershipId is not { } membershipId)
        {
            return SchedulingErrors.OrganizationContextRequired;
        }

        var ownedProfile = await instructors.FindByMembershipAsync(membershipId, cancellationToken);
        if (ownedProfile is null)
        {
            return SchedulingErrors.InstructorNotFound;
        }

        var startsAt = command.StartsAt.ToUniversalTime();

        // Slot asagida BookingClosesAt = StartsAt - BookingCutoffMinutes ile aciliyor.
        // Bu esikten yakina acilan slot, dogar dogmaz kapali olur: ogrenci listesi
        // (EfSchedulingQueries.ListInstructorSlots) tam bu ani filtreledigi icin hic
        // gorunmez, ama egitmenin takviminde durur ve saati bos gostermez. Bu yuzden
        // acilis, gorunurluk esigiyle ayni sinirdan reddedilir.
        if (startsAt <= clock.UtcNow.AddMinutes(LessonSession.BookingCutoffMinutes))
        {
            return SchedulingErrors.SlotTooSoon(LessonSession.BookingCutoffMinutes);
        }

        var course = await catalog.FindCourseAsync(command.CourseId, false, cancellationToken);
        if (course is null)
        {
            return SchedulingErrors.CourseNotFound;
        }

        if (course.Status is not CourseStatus.Published)
        {
            return SchedulingErrors.CourseNotBookable;
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);

        if (!await scheduling.LockInstructorAsync(ownedProfile.Id, cancellationToken))
        {
            return SchedulingErrors.InstructorNotFound;
        }

        var profile = await instructors.FindWithDetailsAsync(ownedProfile.Id, cancellationToken);
        if (profile is null || profile.Status is not InstructorStatus.Active)
        {
            return SchedulingErrors.InstructorNotFound;
        }

        if (!profile.Subjects.Any(subject =>
                subject.SubjectId == course.SubjectId
                && subject.Status == InstructorSubjectStatus.Active))
        {
            return SchedulingErrors.InstructorSubjectMismatch;
        }

        const int slotDurationMinutes = 60;
        var endsAt = startsAt.AddMinutes(slotDurationMinutes);
        if (await scheduling.HasInstructorConflictAsync(
                profile.Id,
                startsAt,
                endsAt,
                null,
                cancellationToken))
        {
            return SchedulingErrors.InstructorBusy;
        }

        var session = LessonSession.Create(
            organizationId,
            course.Id,
            SessionType.Private,
            startsAt,
            endsAt,
            capacity: 1,
            minimumParticipants: 1);

        session.AssignInstructor(profile.Id, SessionInstructorRole.Lead);
        // Rezervasyon ders baslangicina yarim saat kala kapanir.
        session.SetBookingWindow(
            null,
            startsAt.AddMinutes(-LessonSession.BookingCutoffMinutes),
            null);

        var policy = await cancellationPolicies.GetCurrentAsync(cancellationToken);
        if (policy.IsFailure)
        {
            return policy.Error;
        }

        session.ApplyCancellationPolicy(
            policy.Value.StudentRefundCutoffMinutes,
            policy.Value.InstructorPenaltyCutoffMinutes,
            policy.Value.Version);

        scheduling.AddSession(session);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new OpenInstructorSlotResult(
            session.Id,
            course.Id,
            startsAt,
            endsAt,
            slotDurationMinutes);
    }
}
