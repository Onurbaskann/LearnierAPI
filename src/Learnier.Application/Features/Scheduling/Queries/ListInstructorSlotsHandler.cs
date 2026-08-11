using FluentValidation;
using Learnier.Application.Common.Abstractions;
using Learnier.Application.Common.Results;
using Learnier.Domain.Catalog;
using Learnier.Domain.Teaching;

namespace Learnier.Application.Features.Scheduling.Queries;

public sealed record ListInstructorSlotsQuery(
    Guid InstructorProfileId,
    Guid CourseId,
    DateTimeOffset From,
    DateTimeOffset Until);

public sealed record InstructorSlotListItem(
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    bool IsAvailable);

internal sealed class ListInstructorSlotsValidator : AbstractValidator<ListInstructorSlotsQuery>
{
    public ListInstructorSlotsValidator()
    {
        RuleFor(query => query.InstructorProfileId)
            .NotEmpty().WithErrorCode("scheduling.instructor_required");

        RuleFor(query => query.CourseId)
            .NotEmpty().WithErrorCode("scheduling.course_required");

        RuleFor(query => query.Until)
            .GreaterThan(query => query.From)
            .WithErrorCode("scheduling.slot_range_invalid");

        RuleFor(query => query.Until - query.From)
            .LessThanOrEqualTo(TimeSpan.FromDays(14))
            .WithErrorCode("scheduling.slot_range_too_large");
    }
}

public sealed class ListInstructorSlotsHandler(
    IInstructorRepository instructors,
    IInstructorQueries instructorQueries,
    ICatalogRepository catalog,
    ISchedulingRepository scheduling,
    IClock clock)
{
    public async Task<Result<IReadOnlyList<InstructorSlotListItem>>> Handle(
        ListInstructorSlotsQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (query.Until <= query.From)
        {
            return Error.Validation("scheduling.slot_range_invalid");
        }

        if (query.Until - query.From > TimeSpan.FromDays(14))
        {
            return Error.Validation("scheduling.slot_range_too_large");
        }

        var profile = await instructors.FindWithDetailsAsync(
            query.InstructorProfileId,
            cancellationToken);
        if (profile is null || profile.Status is not InstructorStatus.Active)
        {
            return SchedulingErrors.InstructorNotFound;
        }

        var course = await catalog.FindCourseAsync(
            query.CourseId,
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

        if (!profile.Subjects.Any(subject =>
                subject.SubjectId == course.SubjectId
                && subject.Status == InstructorSubjectStatus.Active))
        {
            return SchedulingErrors.InstructorSubjectMismatch;
        }

        TimeZoneInfo zone;
        try
        {
            zone = TimeZoneInfo.FindSystemTimeZoneById(profile.TimeZoneId);
        }
        catch (Exception exception) when (
            exception is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return SchedulingErrors.InstructorUnavailable;
        }

        var from = query.From.ToUniversalTime();
        var until = query.Until.ToUniversalTime();
        var firstDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(from, zone).DateTime);
        var lastDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(until, zone).DateTime);
        var overrides = await instructorQueries.ListOverridesAsync(
            profile.Id,
            firstDate,
            cancellationToken);

        var candidates = new HashSet<DateTimeOffset>();
        for (var date = firstDate; date <= lastDate; date = date.AddDays(1))
        {
            foreach (var availability in profile.Availabilities.Where(item =>
                         item.DayOfWeek == date.DayOfWeek
                         && item.ValidFrom <= date
                         && (item.ValidUntil is null || item.ValidUntil >= date)))
            {
                AddCandidates(
                    candidates,
                    date,
                    availability.StartLocalTime,
                    availability.EndLocalTime,
                    course.DefaultDurationMinutes,
                    profile.TimeZoneId);
            }

            foreach (var addition in overrides.Where(item =>
                         item.OverrideDate == date
                         && item.OverrideType == AvailabilityOverrideType.Available))
            {
                AddCandidates(
                    candidates,
                    date,
                    addition.StartLocalTime ?? TimeOnly.MinValue,
                    addition.EndLocalTime ?? new TimeOnly(23, 59, 59),
                    course.DefaultDurationMinutes,
                    profile.TimeZoneId);
            }
        }

        var visible = candidates
            .Where(start => start >= from && start >= clock.UtcNow)
            .Where(start => start.AddMinutes(course.DefaultDurationMinutes) <= until)
            .OrderBy(start => start)
            .ToList();

        var result = new List<InstructorSlotListItem>(visible.Count);
        foreach (var startsAt in visible)
        {
            var endsAt = startsAt.AddMinutes(course.DefaultDurationMinutes);
            if (!InstructorSlotPolicy.IsAllowed(profile, overrides, startsAt, endsAt))
            {
                continue;
            }

            var busy = await scheduling.HasInstructorConflictAsync(
                profile.Id,
                startsAt,
                endsAt,
                excludeSessionId: null,
                cancellationToken);

            result.Add(new InstructorSlotListItem(startsAt, endsAt, !busy));
        }

        return result;
    }

    private static void AddCandidates(
        HashSet<DateTimeOffset> candidates,
        DateOnly date,
        TimeOnly rangeStart,
        TimeOnly rangeEnd,
        int durationMinutes,
        string timeZoneId)
    {
        for (var time = rangeStart;
             time.AddMinutes(durationMinutes) <= rangeEnd;
             time = time.AddMinutes(durationMinutes))
        {
            if (InstructorSlotPolicy.ToUtc(date, time, timeZoneId) is { } startsAt)
            {
                candidates.Add(startsAt);
            }
        }
    }
}
