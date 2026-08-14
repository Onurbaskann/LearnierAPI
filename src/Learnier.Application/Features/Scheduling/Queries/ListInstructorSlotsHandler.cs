using FluentValidation;
using Learnier.Application.Common.Abstractions;
using Learnier.Application.Common.Results;
using Learnier.Domain.Catalog;
using Learnier.Domain.Teaching;

namespace Learnier.Application.Features.Scheduling.Queries;

public sealed record ListInstructorSlotsQuery(
    Guid InstructorProfileId,
    Guid? CourseId,
    DateTimeOffset From,
    DateTimeOffset Until,
    int? LessonDurationMinutes = null);

public sealed record InstructorSlotListItem(
    Guid SessionId,
    Guid CourseId,
    string CourseTitle,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    int LessonDurationMinutes,
    bool IsAvailable);

internal sealed class ListInstructorSlotsValidator : AbstractValidator<ListInstructorSlotsQuery>
{
    public ListInstructorSlotsValidator()
    {
        RuleFor(query => query.InstructorProfileId)
            .NotEmpty().WithErrorCode("scheduling.instructor_required");

        RuleFor(query => query.Until)
            .GreaterThan(query => query.From)
            .WithErrorCode("scheduling.slot_range_invalid");

        RuleFor(query => query.Until - query.From)
            .LessThanOrEqualTo(TimeSpan.FromDays(90))
            .WithErrorCode("scheduling.slot_range_too_large");

        RuleFor(query => query.LessonDurationMinutes)
            .Must(duration => duration is null or 30 or 50)
            .WithErrorCode("scheduling.lesson_duration_invalid");
    }
}

/// <summary>
/// Egitmenin elle actigi birebir oturumlari listeler. Haftalik uygunluk kayitlari
/// bu sorguda bilincli olarak kullanilmaz.
/// </summary>
public sealed class ListInstructorSlotsHandler(
    IInstructorRepository instructors,
    ICatalogRepository catalog,
    ISchedulingQueries scheduling,
    IClock clock)
{
    public async Task<Result<IReadOnlyList<InstructorSlotListItem>>> Handle(
        ListInstructorSlotsQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (query.Until <= query.From || query.Until - query.From > TimeSpan.FromDays(90))
        {
            return Error.Validation("scheduling.slot_range_invalid");
        }

        if (query.LessonDurationMinutes is not null and not (30 or 50))
        {
            return SchedulingErrors.LessonDurationInvalid;
        }

        var profile = await instructors.FindWithDetailsAsync(
            query.InstructorProfileId,
            cancellationToken);
        if (profile is null || profile.Status is not InstructorStatus.Active)
        {
            return SchedulingErrors.InstructorNotFound;
        }

        if (query.CourseId is { } courseId)
        {
            var course = await catalog.FindCourseAsync(courseId, false, cancellationToken);
            if (course is null)
            {
                return SchedulingErrors.CourseNotFound;
            }

            if (course.Status is not CourseStatus.Published)
            {
                return SchedulingErrors.CourseNotBookable;
            }

            if (!profile.Subjects.Any(subject =>
                    subject.SubjectId == course.SubjectId
                    && subject.Status == InstructorSubjectStatus.Active))
            {
                return SchedulingErrors.InstructorSubjectMismatch;
            }
        }

        var from = query.From.ToUniversalTime();
        var until = query.Until.ToUniversalTime();
        var visibleFrom = from > clock.UtcNow ? from : clock.UtcNow;

        var result = await scheduling.ListInstructorSlotsAsync(
            profile.Id,
            query.CourseId,
            visibleFrom,
            until,
            cancellationToken);

        return Result.Success<IReadOnlyList<InstructorSlotListItem>>(
            query.LessonDurationMinutes is { } duration
                ? result.Where(slot => slot.LessonDurationMinutes == duration).ToList()
                : result);
    }
}

/// <summary>Aktif uyeligin egitmen profilindeki manuel slotlari listeler.</summary>
public sealed class ListMyInstructorSlotsHandler(
    ICurrentTenant currentTenant,
    IInstructorRepository instructors,
    ListInstructorSlotsHandler slots)
{
    public async Task<Result<IReadOnlyList<InstructorSlotListItem>>> Handle(
        Guid? courseId,
        DateTimeOffset from,
        DateTimeOffset until,
        CancellationToken cancellationToken)
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

        return await slots.Handle(
            new ListInstructorSlotsQuery(profile.Id, courseId, from, until),
            cancellationToken);
    }
}
