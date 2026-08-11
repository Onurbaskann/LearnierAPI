using Learnier.Application.Features.Teaching.Queries;
using Learnier.Domain.Teaching;

namespace Learnier.Application.Features.Scheduling;

internal static class InstructorSlotPolicy
{
    public static bool IsAllowed(
        InstructorProfile profile,
        IReadOnlyList<AvailabilityOverrideDetail> overrides,
        DateTimeOffset startsAt,
        DateTimeOffset endsAt)
    {
        TimeZoneInfo zone;
        try
        {
            zone = TimeZoneInfo.FindSystemTimeZoneById(profile.TimeZoneId);
        }
        catch (Exception exception) when (
            exception is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return false;
        }

        var localStart = TimeZoneInfo.ConvertTime(startsAt, zone);
        var localEnd = TimeZoneInfo.ConvertTime(endsAt, zone);

        if (localStart.Date != localEnd.Date)
        {
            return false;
        }

        var date = DateOnly.FromDateTime(localStart.DateTime);
        var start = TimeOnly.FromDateTime(localStart.DateTime);
        var end = TimeOnly.FromDateTime(localEnd.DateTime);
        var duration = endsAt - startsAt;

        var baseMatch = profile.Availabilities.Any(availability =>
            availability.DayOfWeek == localStart.DayOfWeek
            && availability.ValidFrom <= date
            && (availability.ValidUntil is null || availability.ValidUntil >= date)
            && FitsRange(
                start,
                end,
                availability.StartLocalTime,
                availability.EndLocalTime,
                duration));

        var additions = overrides.Where(item =>
            item.OverrideDate == date
            && item.OverrideType == AvailabilityOverrideType.Available);

        var added = additions.Any(item => item.StartLocalTime is null
            ? IsAligned(start, TimeOnly.MinValue, duration)
            : FitsRange(
                start,
                end,
                item.StartLocalTime.Value,
                item.EndLocalTime!.Value,
                duration));

        if (!baseMatch && !added)
        {
            return false;
        }

        return !overrides.Any(item =>
            item.OverrideDate == date
            && item.OverrideType == AvailabilityOverrideType.Unavailable
            && (item.StartLocalTime is null
                || RangesOverlap(
                    start,
                    end,
                    item.StartLocalTime.Value,
                    item.EndLocalTime!.Value)));
    }

    public static DateTimeOffset? ToUtc(
        DateOnly date,
        TimeOnly time,
        string timeZoneId)
    {
        try
        {
            var zone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            var local = DateTime.SpecifyKind(date.ToDateTime(time), DateTimeKind.Unspecified);
            if (zone.IsInvalidTime(local))
            {
                return null;
            }

            return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, zone), TimeSpan.Zero);
        }
        catch (Exception exception) when (
            exception is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return null;
        }
    }

    private static bool FitsRange(
        TimeOnly start,
        TimeOnly end,
        TimeOnly rangeStart,
        TimeOnly rangeEnd,
        TimeSpan duration)
        => start >= rangeStart
           && end <= rangeEnd
           && IsAligned(start, rangeStart, duration);

    private static bool IsAligned(TimeOnly start, TimeOnly rangeStart, TimeSpan duration)
    {
        var elapsed = start.ToTimeSpan() - rangeStart.ToTimeSpan();
        return elapsed >= TimeSpan.Zero
               && duration > TimeSpan.Zero
               && elapsed.Ticks % duration.Ticks == 0;
    }

    private static bool RangesOverlap(
        TimeOnly leftStart,
        TimeOnly leftEnd,
        TimeOnly rightStart,
        TimeOnly rightEnd)
        => leftStart < rightEnd && leftEnd > rightStart;
}
