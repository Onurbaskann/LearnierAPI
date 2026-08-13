using FluentValidation;
using Learnier.Application.Common.Abstractions;
using Learnier.Application.Common.Results;
using Learnier.Domain.Scheduling;

namespace Learnier.Application.Features.Scheduling.Commands.CompleteSession;

public sealed record CompleteSessionAttendance(
    Guid BookingId,
    AttendanceStatus Status,
    int AttendedMinutes,
    DateTimeOffset? JoinedAt = null,
    DateTimeOffset? LeftAt = null);

public sealed record CompleteSessionCommand(
    Guid SessionId,
    IReadOnlyList<CompleteSessionAttendance> Attendances);

public sealed record CompleteSessionResult(int CompletedBookingCount);

internal sealed class CompleteSessionValidator : AbstractValidator<CompleteSessionCommand>
{
    public CompleteSessionValidator()
    {
        RuleFor(command => command.SessionId)
            .NotEmpty().WithErrorCode("scheduling.session_required");

        RuleFor(command => command.Attendances)
            .NotNull().WithErrorCode("scheduling.attendance_required");

        RuleForEach(command => command.Attendances).ChildRules(attendance =>
        {
            attendance.RuleFor(item => item.BookingId)
                .NotEmpty().WithErrorCode("scheduling.booking_required");
            attendance.RuleFor(item => item.Status)
                .IsInEnum().WithErrorCode("scheduling.attendance_status_invalid");
            attendance.RuleFor(item => item.AttendedMinutes)
                .GreaterThanOrEqualTo(0).WithErrorCode("scheduling.attended_minutes_invalid");
            attendance.RuleFor(item => item.AttendedMinutes)
                .Equal(0).When(item => item.Status == AttendanceStatus.Absent)
                .WithErrorCode("scheduling.absent_minutes_invalid");
            attendance.RuleFor(item => item.AttendedMinutes)
                .GreaterThan(0).When(item => item.Status != AttendanceStatus.Absent)
                .WithErrorCode("scheduling.attended_minutes_required");
            attendance.RuleFor(item => item.LeftAt)
                .GreaterThanOrEqualTo(item => item.JoinedAt!.Value)
                .When(item => item.JoinedAt is not null && item.LeftAt is not null)
                .WithErrorCode("scheduling.attendance_time_range_invalid");
        });

        RuleFor(command => command.Attendances)
            .Must(items => items.Select(item => item.BookingId).Distinct().Count() == items.Count)
            .When(command => command.Attendances is not null)
            .WithErrorCode("scheduling.attendance_booking_duplicate");
    }
}

/// <summary>Dersi, katilim kayitlarini ve kredi tuketimlerini tek islemde tamamlar.</summary>
public sealed class CompleteSessionHandler(
    ISchedulingRepository scheduling,
    IInstructorRepository instructors,
    IBookingEntitlementPolicy entitlements,
    IInstructorCompensationService compensation,
    ICurrentUser currentUser,
    ICurrentTenant currentTenant,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    public async Task<Result<CompleteSessionResult>> Handle(
        CompleteSessionCommand command,
        bool canCompleteAnySession,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!currentTenant.HasTenant || currentUser.UserId is not { } actingUserId)
        {
            return SchedulingErrors.OrganizationContextRequired;
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        var session = await scheduling.FindSessionForUpdateAsync(command.SessionId, cancellationToken);

        if (session is null)
        {
            return SchedulingErrors.SessionNotFound;
        }

        var bookings = await scheduling.ListActiveBookingsAsync(session.Id, cancellationToken);
        var participantBookings = bookings
            .Where(booking => booking.Status is not BookingStatus.Waitlisted)
            .ToList();

        if (session.Status is LessonSessionStatus.Completed)
        {
            return new CompleteSessionResult(participantBookings.Count);
        }

        if (session.Status is LessonSessionStatus.Cancelled)
        {
            return SchedulingErrors.SessionNotCompletable;
        }

        if (clock.UtcNow < session.EndsAt)
        {
            return SchedulingErrors.SessionNotEnded;
        }

        if (!canCompleteAnySession)
        {
            if (currentTenant.MembershipId is not { } membershipId)
            {
                return SchedulingErrors.OrganizationContextRequired;
            }

            var profile = await instructors.FindByMembershipAsync(membershipId, cancellationToken);
            session = await scheduling.FindSessionAsync(session.Id, true, cancellationToken);

            if (profile is null || session is null
                || session.Instructors.All(item => item.InstructorProfileId != profile.Id))
            {
                return SchedulingErrors.SessionNotOwned;
            }
        }

        if (participantBookings.Count is 0)
        {
            return SchedulingErrors.SessionHasNoReservations;
        }

        var attendanceByBooking = command.Attendances.ToDictionary(item => item.BookingId);
        if (attendanceByBooking.Count != participantBookings.Count
            || participantBookings.Any(booking => !attendanceByBooking.ContainsKey(booking.Id)))
        {
            return SchedulingErrors.AttendanceSetMismatch;
        }

        foreach (var booking in participantBookings)
        {
            if (booking.Attendance is not null)
            {
                return SchedulingErrors.SessionNotCompletable;
            }

            var input = attendanceByBooking[booking.Id];
            var attendance = SessionAttendance.Create(
                booking.Id,
                input.Status,
                input.AttendedMinutes,
                input.JoinedAt?.ToUniversalTime(),
                input.LeftAt?.ToUniversalTime(),
                actingUserId);

            scheduling.AddAttendance(attendance);

            if (input.Status is AttendanceStatus.Absent)
            {
                booking.MarkNoShow();
            }
            else
            {
                booking.MarkAttended();
            }

            var consumption = await entitlements.ConsumeAsync(booking, cancellationToken);
            if (consumption.IsFailure)
            {
                return consumption.Error;
            }
        }

        var earnings = await compensation.CreateEarningsAsync(session.Id, cancellationToken);
        if (earnings.IsFailure)
        {
            return earnings.Error;
        }

        session.Complete();
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new CompleteSessionResult(participantBookings.Count);
    }
}
