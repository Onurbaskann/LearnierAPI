using FluentValidation;
using Learnier.Application.Common.Abstractions;
using Learnier.Application.Common.Results;
using Learnier.Domain.Scheduling;

namespace Learnier.Application.Features.Scheduling.Commands.CreateSession;

/// <param name="StartsAt">UTC baslangic. Istemci kendi saat diliminden cevirir.</param>
/// <param name="CancellationDeadlineAt">Bu ana kadar iptal edilirse hak iade edilir.</param>
public sealed record CreateSessionCommand(
    Guid CourseId,
    SessionType SessionType,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    int Capacity,
    int MinimumParticipants,
    Guid? ClassGroupId = null,
    Guid? CourseLessonId = null,
    DateTimeOffset? BookingOpensAt = null,
    DateTimeOffset? BookingClosesAt = null,
    DateTimeOffset? CancellationDeadlineAt = null);

public sealed record CreateSessionResult(Guid SessionId, LessonSessionStatus Status);

internal sealed class CreateSessionValidator : AbstractValidator<CreateSessionCommand>
{
    public CreateSessionValidator()
    {
        RuleFor(c => c.CourseId)
            .NotEmpty().WithErrorCode("scheduling.course_required");

        RuleFor(c => c.SessionType)
            .IsInEnum().WithErrorCode("scheduling.session_type_invalid");

        RuleFor(c => c.EndsAt)
            .GreaterThan(c => c.StartsAt)
            .WithErrorCode("scheduling.session_time_range_invalid");

        RuleFor(c => c.Capacity)
            .InclusiveBetween(1, 1000).WithErrorCode("scheduling.capacity_invalid");

        RuleFor(c => c.MinimumParticipants)
            .GreaterThanOrEqualTo(0).WithErrorCode("scheduling.minimum_participants_invalid")
            .LessThanOrEqualTo(c => c.Capacity)
            .WithErrorCode("scheduling.minimum_exceeds_capacity");

        RuleFor(c => c.BookingClosesAt)
            .GreaterThanOrEqualTo(c => c.BookingOpensAt!.Value)
            .WithErrorCode("scheduling.booking_window_invalid")
            .When(c => c.BookingOpensAt is not null && c.BookingClosesAt is not null);
    }
}

/// <summary>
/// Takvime yeni oturum ekler.
/// </summary>
public sealed class CreateSessionHandler(
    ISchedulingRepository scheduling,
    ICatalogRepository catalog,
    ICurrentTenant currentTenant,
    IUnitOfWork unitOfWork)
{
    public async Task<Result<CreateSessionResult>> Handle(
        CreateSessionCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (currentTenant.OrganizationId is not { } organizationId)
        {
            return SchedulingErrors.OrganizationContextRequired;
        }

        var course = await catalog.FindCourseAsync(command.CourseId, includeModules: false, cancellationToken);

        if (course is null)
        {
            return SchedulingErrors.CourseNotFound;
        }

        if (command.ClassGroupId is { } classGroupId)
        {
            var classGroup = await scheduling.FindClassGroupAsync(
                classGroupId, includeMembers: false, cancellationToken);

            if (classGroup is null)
            {
                return SchedulingErrors.ClassGroupNotFound;
            }
        }

        var session = LessonSession.Create(
            organizationId,
            course.Id,
            command.SessionType,
            command.StartsAt.ToUniversalTime(),
            command.EndsAt.ToUniversalTime(),
            command.Capacity,
            command.MinimumParticipants,
            command.ClassGroupId,
            command.CourseLessonId);

        session.SetBookingWindow(
            command.BookingOpensAt?.ToUniversalTime(),
            command.BookingClosesAt?.ToUniversalTime(),
            command.CancellationDeadlineAt?.ToUniversalTime());

        scheduling.AddSession(session);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new CreateSessionResult(session.Id, session.Status);
    }
}
