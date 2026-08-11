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
        var monthDurations = await sessions
            .Where(s => s.Status == LessonSessionStatus.Completed
                        && s.StartsAt >= monthStartsAt
                        && s.StartsAt < monthEndsAt)
            .Select(s => new { s.StartsAt, s.EndsAt })
            .ToListAsync(cancellationToken);
        var hourlyRate = profile.DefaultHourlyRate ?? 0m;
        var thisMonthTotal = monthDurations.Sum(
            session => hourlyRate * (decimal)(session.EndsAt - session.StartsAt).TotalHours);
        var averageRating = await context.SessionFeedback
            .AsNoTracking()
            .Where(f => f.TargetInstructorProfileId == profile.Id)
            .Select(f => (double?)f.Rating)
            .AverageAsync(cancellationToken);

        return new InstructorDashboardStats(
            studentCount,
            completedLessons,
            decimal.Round(thisMonthTotal, 2),
            profile.DefaultHourlyRateCurrency ?? "TRY",
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

        var query = context.LessonSessions
            .AsNoTracking()
            .Where(s => s.Status == LessonSessionStatus.Completed)
            .Where(s => s.Instructors.Any(i => i.InstructorProfileId == profile.Id));
        if (from is { } after)
        {
            query = query.Where(s => s.EndsAt >= after);
        }

        if (until is { } before)
        {
            query = query.Where(s => s.StartsAt <= before);
        }

        var sessions = await query
            .OrderByDescending(s => s.StartsAt)
            .Select(s => new
            {
                s.Id,
                s.Course.Title,
                s.StartsAt,
                s.EndsAt,
                LearnerCount = s.Bookings.Count(b =>
                    b.Status == BookingStatus.Reserved
                    || b.Status == BookingStatus.Attended
                    || b.Status == BookingStatus.NoShow)
            })
            .ToListAsync(cancellationToken);
        var hourlyRate = profile.DefaultHourlyRate ?? 0m;
        var currency = profile.DefaultHourlyRateCurrency ?? "TRY";

        return sessions.Select(session => new InstructorEarningListItem(
            session.Id,
            session.Title,
            session.StartsAt,
            session.LearnerCount,
            decimal.Round(
                hourlyRate * (decimal)(session.EndsAt - session.StartsAt).TotalHours, 2),
            currency)).ToList();
    }
}
