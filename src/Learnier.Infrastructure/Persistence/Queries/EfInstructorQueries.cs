using Learnier.Application.Common.Abstractions;
using Learnier.Application.Common.Models;
using Learnier.Application.Features.Teaching.Queries;
using Learnier.Domain.Progress;
using Learnier.Domain.Scheduling;
using Learnier.Domain.Teaching;
using Microsoft.EntityFrameworkCore;

namespace Learnier.Infrastructure.Persistence.Queries;

/// <inheritdoc cref="IInstructorQueries"/>
/// <remarks>
/// Kiraci siniri uyelik uzerinden korunur: profil kendi organizasyon sutununu
/// tasimadigi icin her sorgu uyeliklere baglanir.
/// </remarks>
internal sealed class EfInstructorQueries(AppDbContext context) : IInstructorQueries
{
    public async Task<PagedResult<InstructorListItem>> ListAsync(
        PageRequest page,
        Guid? subjectId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(page);

        var query = context.InstructorProfiles
            .AsNoTracking()
            .Where(p => context.Memberships.Any(m => m.Id == p.MembershipId));

        if (subjectId is { } filterSubjectId)
        {
            query = query.Where(p => p.Subjects.Any(
                s => s.SubjectId == filterSubjectId
                     && s.Status == InstructorSubjectStatus.Active));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            // Ikincil anahtar sayfalar arasi kaymayi engeller.
            .OrderBy(p => p.Membership.User.FirstName)
            .ThenBy(p => p.Id)
            .Skip(page.Skip)
            .Take(page.PageSize)
            .Select(p => new InstructorListItem(
                p.Id,
                p.MembershipId,
                p.Membership.User.FirstName,
                p.Membership.User.LastName,
                p.Headline,
                p.Status,
                p.TimeZoneId,
                p.Subjects
                    .Where(s => s.Status == InstructorSubjectStatus.Active)
                    .Select(s => s.Subject.Name)
                    .Distinct()
                    .ToList()))
            .ToListAsync(cancellationToken);

        return new PagedResult<InstructorListItem>(items, page.Page, page.PageSize, totalCount);
    }

    public async Task<InstructorDetail?> FindDetailAsync(
        Guid profileId,
        CancellationToken cancellationToken)
        => await context.InstructorProfiles
            .AsNoTracking()
            .Where(p => p.Id == profileId)
            .Where(p => context.Memberships.Any(m => m.Id == p.MembershipId))
            .Select(p => new InstructorDetail(
                p.Id,
                p.MembershipId,
                p.Membership.User.FirstName,
                p.Membership.User.LastName,
                p.Headline,
                p.Bio,
                p.Hobbies,
                p.TimeZoneId,
                p.Status,
                p.DefaultHourlyRate,
                p.DefaultHourlyRateCurrency,
                p.Subjects
                    .Select(s => new InstructorSubjectDetail(
                        s.Id,
                        s.SubjectId,
                        s.Subject.Name,
                        s.LevelId,
                        context.Levels
                            .Where(l => l.Id == s.LevelId)
                            .Select(l => l.Code)
                            .FirstOrDefault(),
                        s.Status))
                    .ToList(),
                p.Availabilities
                    .OrderBy(a => a.DayOfWeek)
                    .ThenBy(a => a.StartLocalTime)
                    .Select(a => new InstructorAvailabilityDetail(
                        a.Id,
                        a.DayOfWeek,
                        a.StartLocalTime,
                        a.EndLocalTime,
                        a.TimeZoneId,
                        a.ValidFrom,
                        a.ValidUntil))
                    .ToList()))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<AvailabilityOverrideDetail>> ListOverridesAsync(
        Guid profileId,
        DateOnly from,
        CancellationToken cancellationToken)
        => await context.InstructorAvailabilityOverrides
            .AsNoTracking()
            .Where(o => o.InstructorProfileId == profileId && o.OverrideDate >= from)
            .Where(o => context.InstructorProfiles.Any(
                p => p.Id == o.InstructorProfileId
                     && context.Memberships.Any(m => m.Id == p.MembershipId)))
            .OrderBy(o => o.OverrideDate)
            .Select(o => new AvailabilityOverrideDetail(
                o.Id,
                o.OverrideDate,
                o.StartLocalTime,
                o.EndLocalTime,
                o.OverrideType,
                o.Reason))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<InstructorStudentListItem>?> ListMyStudentsAsync(
        Guid membershipId,
        CancellationToken cancellationToken)
    {
        var profileId = await context.InstructorProfiles
            .Where(p => p.MembershipId == membershipId)
            .Select(p => (Guid?)p.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (profileId is null)
        {
            return null;
        }

        var bookings = await context.SessionBookings
            .AsNoTracking()
            .Where(b => b.Status == BookingStatus.Reserved
                        || b.Status == BookingStatus.Attended
                        || b.Status == BookingStatus.NoShow)
            .Where(b => b.Session.Status != LessonSessionStatus.Cancelled)
            .Where(b => b.Session.Instructors.Any(i => i.InstructorProfileId == profileId))
            .Select(b => new
            {
                b.LearnerUserId,
                b.Learner.FirstName,
                b.Learner.LastName,
                CourseTitle = b.Session.Course.Title,
                b.Session.StartsAt
            })
            .ToListAsync(cancellationToken);

        return bookings
            .GroupBy(b => new { b.LearnerUserId, b.FirstName, b.LastName })
            .Select(group => new InstructorStudentListItem(
                group.Key.LearnerUserId,
                group.Key.FirstName,
                group.Key.LastName,
                group.Select(b => b.CourseTitle).Distinct().Order().ToList(),
                group.Count(),
                group.Max(b => b.StartsAt)))
            .OrderBy(item => item.FirstName)
            .ThenBy(item => item.UserId)
            .ToList();
    }

    public async Task<IReadOnlyList<InstructorScheduleListItem>?> ListMyScheduleAsync(
        Guid membershipId,
        DateTimeOffset? from,
        DateTimeOffset? until,
        CancellationToken cancellationToken)
    {
        var profileId = await context.InstructorProfiles
            .Where(profile => profile.MembershipId == membershipId)
            .Select(profile => (Guid?)profile.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (profileId is null)
        {
            return null;
        }

        var query = context.LessonSessions
            .AsNoTracking()
            .Where(session => session.Status != LessonSessionStatus.Cancelled)
            .Where(session => session.Instructors.Any(instructor =>
                instructor.InstructorProfileId == profileId))
            .Where(session => session.Bookings.Any(booking =>
                booking.Status == BookingStatus.Reserved
                || booking.Status == BookingStatus.Attended
                || booking.Status == BookingStatus.NoShow));

        if (from is { } startsAfter)
        {
            query = query.Where(session => session.EndsAt >= startsAfter);
        }

        if (until is { } startsBefore)
        {
            query = query.Where(session => session.StartsAt <= startsBefore);
        }

        return await query
            .OrderBy(session => session.StartsAt)
            .ThenBy(session => session.Id)
            .Select(session => new InstructorScheduleListItem(
                session.Id,
                session.Course.Title,
                session.StartsAt,
                session.EndsAt,
                session.Status,
                session.Bookings
                    .Where(booking => booking.Status == BookingStatus.Reserved
                                      || booking.Status == BookingStatus.Attended
                                      || booking.Status == BookingStatus.NoShow)
                    .OrderBy(booking => booking.BookedAt)
                    .ThenBy(booking => booking.Id)
                    .Select(booking => new InstructorScheduleLearner(
                        booking.LearnerUserId,
                        booking.Learner.FirstName,
                        booking.Learner.LastName))
                    .ToList()))
            .ToListAsync(cancellationToken);
    }

    public async Task<InstructorDashboardStats?> FindMyDashboardAsync(
        Guid membershipId,
        DateTimeOffset monthStartsAt,
        DateTimeOffset monthEndsAt,
        CancellationToken cancellationToken)
    {
        var profile = await context.InstructorProfiles
            .AsNoTracking()
            .Where(p => p.MembershipId == membershipId)
            .Select(p => new { p.Id, p.DefaultHourlyRate, p.DefaultHourlyRateCurrency })
            .FirstOrDefaultAsync(cancellationToken);
        if (profile is null)
        {
            return null;
        }

        var sessions = context.LessonSessions
            .AsNoTracking()
            .Where(s => s.Instructors.Any(i => i.InstructorProfileId == profile.Id));

        var studentCount = await context.SessionBookings
            .AsNoTracking()
            .Where(b => b.Status == BookingStatus.Reserved
                        || b.Status == BookingStatus.Attended
                        || b.Status == BookingStatus.NoShow)
            .Where(b => b.Session.Status != LessonSessionStatus.Cancelled)
            .Where(b => b.Session.Instructors.Any(i => i.InstructorProfileId == profile.Id))
            .Select(b => b.LearnerUserId)
            .Distinct()
            .CountAsync(cancellationToken);

        var completedLessons = await sessions
            .CountAsync(s => s.Status == LessonSessionStatus.Completed, cancellationToken);
        var monthEarnings = await context.InstructorEarnings
            .AsNoTracking()
            .Where(earning => earning.InstructorProfileId == profile.Id
                              && earning.EarnedAt >= monthStartsAt
                              && earning.EarnedAt < monthEndsAt)
            .Select(earning => new { earning.NetAmount, earning.Currency })
            .ToListAsync(cancellationToken);
        var currency = monthEarnings.Select(item => item.Currency).FirstOrDefault()
            ?? profile.DefaultHourlyRateCurrency
            ?? "TRY";
        var thisMonthTotal = monthEarnings
            .Where(item => item.Currency == currency)
            .Sum(item => item.NetAmount);
        var averageRating = await context.SessionFeedback
            .AsNoTracking()
            .Where(f => f.TargetInstructorProfileId == profile.Id)
            .Select(f => (double?)f.Rating)
            .AverageAsync(cancellationToken);

        return new InstructorDashboardStats(
            studentCount,
            completedLessons,
            decimal.Round(thisMonthTotal, 2),
            currency,
            averageRating);
    }

    public async Task<IReadOnlyList<InstructorEarningListItem>?> ListMyEarningsAsync(
        Guid membershipId,
        DateTimeOffset? from,
        DateTimeOffset? until,
        CancellationToken cancellationToken)
    {
        var profile = await context.InstructorProfiles
            .AsNoTracking()
            .Where(p => p.MembershipId == membershipId)
            .Select(p => new { p.Id, p.DefaultHourlyRate, p.DefaultHourlyRateCurrency })
            .FirstOrDefaultAsync(cancellationToken);
        if (profile is null)
        {
            return null;
        }

        var query = context.InstructorEarnings
            .AsNoTracking()
            .Where(earning => earning.InstructorProfileId == profile.Id);
        if (from is { } after)
        {
            query = query.Where(earning => earning.EarnedAt >= after);
        }

        if (until is { } before)
        {
            query = query.Where(earning => earning.EarnedAt <= before);
        }

        return await query
            .OrderByDescending(earning => earning.EarnedAt)
            .Select(earning => new InstructorEarningListItem(
                earning.SessionId,
                context.LessonSessions
                    .Where(session => session.Id == earning.SessionId)
                    .Select(session => session.Course.Title)
                    .Single(),
                context.LessonSessions
                    .Where(session => session.Id == earning.SessionId)
                    .Select(session => session.StartsAt)
                    .Single(),
                context.SessionBookings.Count(booking =>
                    booking.SessionId == earning.SessionId
                    && (booking.Status == BookingStatus.Attended
                        || booking.Status == BookingStatus.NoShow)),
                earning.NetAmount,
                earning.Currency,
                earning.GrossAmount,
                earning.PenaltyPercentage,
                earning.PenaltyAmount))
            .ToListAsync(cancellationToken);
    }
}
