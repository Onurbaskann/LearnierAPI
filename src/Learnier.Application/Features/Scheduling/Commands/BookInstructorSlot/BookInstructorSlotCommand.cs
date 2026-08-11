using FluentValidation;
using Learnier.Application.Common.Abstractions;
using Learnier.Application.Common.Results;
using Learnier.Domain.Catalog;
using Learnier.Domain.Scheduling;
using Learnier.Domain.Teaching;

namespace Learnier.Application.Features.Scheduling.Commands.BookInstructorSlot;

public sealed record BookInstructorSlotCommand(
    Guid InstructorProfileId,
    Guid CourseId,
    DateTimeOffset StartsAt);

public sealed record BookInstructorSlotResult(
    Guid BookingId,
    Guid SessionId,
    BookingStatus Status,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt);

internal sealed class BookInstructorSlotValidator : AbstractValidator<BookInstructorSlotCommand>
{
    public BookInstructorSlotValidator()
    {
        RuleFor(command => command.InstructorProfileId)
            .NotEmpty().WithErrorCode("scheduling.instructor_required");

        RuleFor(command => command.CourseId)
            .NotEmpty().WithErrorCode("scheduling.course_required");
    }
}

/// <summary>
/// Haftalik uygunluktaki tek bir slottan birebir oturum ve rezervasyonu atomik olusturur.
/// </summary>
public sealed class BookInstructorSlotHandler(
    ISchedulingRepository scheduling,
    IInstructorRepository instructors,
    IInstructorQueries instructorQueries,
    ICatalogRepository catalog,
    IBookingEntitlementPolicy entitlements,
    ICurrentUser currentUser,
    ICurrentTenant currentTenant,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    public async Task<Result<BookInstructorSlotResult>> Handle(
        BookInstructorSlotCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (currentTenant.OrganizationId is not { } organizationId)
        {
            return SchedulingErrors.OrganizationContextRequired;
        }

        if (currentUser.UserId is not { } learnerUserId)
        {
            return Error.Unauthorized("common.unauthorized");
        }

        var startsAt = command.StartsAt.ToUniversalTime();
        if (startsAt <= clock.UtcNow)
        {
            return SchedulingErrors.InstructorUnavailable;
        }

        var course = await catalog.FindCourseAsync(
            command.CourseId,
            includeModules: false,
            cancellationToken);

        if (course is null)
        {
            return SchedulingErrors.CourseNotFound;
        }

        if (course.Status is not CourseStatus.Published)
        {
            return SchedulingErrors.CourseNotBookable;
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);

        if (!await scheduling.LockInstructorAsync(command.InstructorProfileId, cancellationToken))
        {
            return SchedulingErrors.InstructorNotFound;
        }

        var profile = await instructors.FindWithDetailsAsync(
            command.InstructorProfileId,
            cancellationToken);

        if (profile is null || profile.Status is not InstructorStatus.Active)
        {
            return SchedulingErrors.InstructorNotFound;
        }

        var teachesSubject = profile.Subjects.Any(subject =>
            subject.SubjectId == course.SubjectId
            && subject.Status == InstructorSubjectStatus.Active);

        if (!teachesSubject)
        {
            return SchedulingErrors.InstructorSubjectMismatch;
        }

        var endsAt = startsAt.AddMinutes(course.DefaultDurationMinutes);
        DateOnly localDate;
        try
        {
            localDate = DateOnly.FromDateTime(
                TimeZoneInfo.ConvertTime(
                    startsAt,
                    TimeZoneInfo.FindSystemTimeZoneById(profile.TimeZoneId)).DateTime);
        }
        catch (Exception exception) when (
            exception is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return SchedulingErrors.InstructorUnavailable;
        }
        var overrides = await instructorQueries.ListOverridesAsync(
            profile.Id,
            localDate,
            cancellationToken);

        if (!InstructorSlotPolicy.IsAllowed(profile, overrides, startsAt, endsAt))
        {
            return SchedulingErrors.InstructorUnavailable;
        }

        if (await scheduling.HasInstructorConflictAsync(
                profile.Id,
                startsAt,
                endsAt,
                excludeSessionId: null,
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
        session.SetBookingWindow(
            opensAt: null,
            closesAt: startsAt,
            cancellationDeadlineAt: startsAt.AddHours(-6));

        var grant = await entitlements.AuthorizeAsync(
            learnerUserId,
            session,
            cancellationToken);

        if (grant.IsFailure)
        {
            return grant.Error;
        }

        var booking = session.Book(
            learnerUserId,
            learnerUserId,
            grant.Value.AccessSource,
            clock.UtcNow,
            reservedSeatCount: 0,
            grant.Value.SubscriptionId);

        session.Confirm(reservedSeatCount: 1);
        scheduling.AddSession(session);
        scheduling.AddBooking(booking);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new BookInstructorSlotResult(
            booking.Id,
            session.Id,
            booking.Status,
            session.StartsAt,
            session.EndsAt);
    }
}
