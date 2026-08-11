using Learnier.Application.Common.Abstractions;
using Learnier.Application.Common.Models;
using Learnier.Application.Features.Scheduling.Queries;
using Learnier.Domain.Scheduling;
using Microsoft.EntityFrameworkCore;

namespace Learnier.Infrastructure.Persistence.Queries;

internal sealed class EfSchedulingQueries(AppDbContext context) : ISchedulingQueries
{
    public async Task<PagedResult<ClassGroupListItem>> ListClassGroupsAsync(
        PageRequest page,
        Guid? courseId,
        ClassGroupStatus? status,
        CancellationToken cancellationToken)
    {
        var query = context.ClassGroups.AsNoTracking();

        if (courseId is { } selectedCourseId)
        {
            query = query.Where(g => g.CourseId == selectedCourseId);
        }

        if (status is { } selectedStatus)
        {
            query = query.Where(g => g.Status == selectedStatus);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(g => g.Name)
            .ThenBy(g => g.Id)
            .Skip(page.Skip)
            .Take(page.PageSize)
            .Select(g => new ClassGroupListItem(
                g.Id,
                g.CourseId,
                g.Course.Title,
                g.Name,
                g.DeliveryType,
                g.StartsOn,
                g.EndsOn,
                g.Capacity,
                g.Status,
                g.Members.Count(m => m.Status == ClassGroupMemberStatus.Active)))
            .ToListAsync(cancellationToken);

        return new PagedResult<ClassGroupListItem>(
            items, page.Page, page.PageSize, totalCount);
    }

    public async Task<ClassGroupDetail?> FindClassGroupDetailAsync(
        Guid classGroupId,
        CancellationToken cancellationToken)
        => await context.ClassGroups
            .AsNoTracking()
            .Where(g => g.Id == classGroupId)
            .Select(g => new ClassGroupDetail(
                g.Id,
                g.CourseId,
                g.Course.Title,
                g.Name,
                g.DeliveryType,
                g.StartsOn,
                g.EndsOn,
                g.Capacity,
                g.Status,
                g.Members
                    .OrderBy(m => m.EnrolledAt)
                    .ThenBy(m => m.Id)
                    .Select(m => new ClassGroupMemberDetail(
                        m.Id,
                        m.LearnerUserId,
                        m.Learner.FirstName,
                        m.Learner.LastName,
                        m.Status,
                        m.EnrolledAt,
                        m.LeftAt))
                    .ToList()))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<PagedResult<SessionListItem>> ListSessionsAsync(
        PageRequest page,
        Guid? courseId,
        DateTimeOffset? from,
        DateTimeOffset? until,
        LessonSessionStatus? status,
        CancellationToken cancellationToken)
    {
        var query = context.LessonSessions.AsNoTracking();

        if (courseId is { } selectedCourseId)
        {
            query = query.Where(s => s.CourseId == selectedCourseId);
        }

        if (from is { } startsAfter)
        {
            query = query.Where(s => s.EndsAt >= startsAfter);
        }

        if (until is { } startsBefore)
        {
            query = query.Where(s => s.StartsAt <= startsBefore);
        }

        if (status is { } selectedStatus)
        {
            query = query.Where(s => s.Status == selectedStatus);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(s => s.StartsAt)
            .ThenBy(s => s.Id)
            .Skip(page.Skip)
            .Take(page.PageSize)
            .Select(s => new SessionListItem(
                s.Id,
                s.CourseId,
                s.Course.Title,
                s.ClassGroupId,
                s.SessionType,
                s.StartsAt,
                s.EndsAt,
                s.Capacity,
                s.MinimumParticipants,
                s.Status,
                s.Bookings.Count(b => b.Status == BookingStatus.Reserved
                                      || b.Status == BookingStatus.Attended
                                      || b.Status == BookingStatus.NoShow),
                s.Bookings.Count(b => b.Status == BookingStatus.Waitlisted)))
            .ToListAsync(cancellationToken);

        return new PagedResult<SessionListItem>(
            items, page.Page, page.PageSize, totalCount);
    }

    public async Task<SessionDetail?> FindSessionDetailAsync(
        Guid sessionId,
        CancellationToken cancellationToken)
        => await context.LessonSessions
            .AsNoTracking()
            .Where(s => s.Id == sessionId)
            .Select(s => new SessionDetail(
                s.Id,
                s.CourseId,
                s.Course.Title,
                s.ClassGroupId,
                s.CourseLessonId,
                s.SessionType,
                s.StartsAt,
                s.EndsAt,
                s.Capacity,
                s.MinimumParticipants,
                s.Status,
                s.MeetingProvider,
                s.MeetingReference,
                s.BookingOpensAt,
                s.BookingClosesAt,
                s.CancellationDeadlineAt,
                s.CancellationReason,
                s.Bookings.Count(b => b.Status == BookingStatus.Reserved
                                      || b.Status == BookingStatus.Attended
                                      || b.Status == BookingStatus.NoShow),
                s.Bookings.Count(b => b.Status == BookingStatus.Waitlisted),
                s.Instructors
                    .OrderBy(i => i.Role)
                    .ThenBy(i => i.Id)
                    .Select(i => new SessionInstructorDetail(
                        i.InstructorProfileId,
                        i.InstructorProfile.Membership.User.FirstName,
                        i.InstructorProfile.Membership.User.LastName,
                        i.Role))
                    .ToList()))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<InstructorSlotListItem>> ListInstructorSlotsAsync(
        Guid instructorProfileId,
        Guid? courseId,
        DateTimeOffset from,
        DateTimeOffset until,
        CancellationToken cancellationToken)
    {
        var query = context.LessonSessions
            .AsNoTracking()
            .Where(session => session.SessionType == SessionType.Private)
            .Where(session => session.Status != LessonSessionStatus.Cancelled
                              && session.Status != LessonSessionStatus.Completed)
            .Where(session => session.StartsAt >= from && session.StartsAt < until)
            .Where(session => session.Instructors.Any(instructor =>
                instructor.InstructorProfileId == instructorProfileId));

        if (courseId is { } selectedCourseId)
        {
            query = query.Where(session => session.CourseId == selectedCourseId);
        }

        return await query
            .OrderBy(session => session.StartsAt)
            .ThenBy(session => session.Id)
            .Select(session => new InstructorSlotListItem(
                session.Id,
                session.CourseId,
                session.Course.Title,
                session.StartsAt,
                session.EndsAt,
                session.Bookings.Count(booking => booking.Status == BookingStatus.Reserved
                                                  || booking.Status == BookingStatus.Attended
                                                  || booking.Status == BookingStatus.NoShow)
                    < session.Capacity))
            .ToListAsync(cancellationToken);
    }

    public async Task<PagedResult<LearnerBookingListItem>> ListLearnerBookingsAsync(
        PageRequest page,
        Guid learnerUserId,
        DateTimeOffset? from,
        DateTimeOffset? until,
        BookingStatus? status,
        CancellationToken cancellationToken)
    {
        var query = context.SessionBookings
            .AsNoTracking()
            .Where(b => b.LearnerUserId == learnerUserId);

        if (from is { } startsAfter)
        {
            query = query.Where(b => b.Session.EndsAt >= startsAfter);
        }

        if (until is { } startsBefore)
        {
            query = query.Where(b => b.Session.StartsAt <= startsBefore);
        }

        if (status is { } selectedStatus)
        {
            query = query.Where(b => b.Status == selectedStatus);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(b => b.Session.StartsAt)
            .ThenBy(b => b.Id)
            .Skip(page.Skip)
            .Take(page.PageSize)
            .Select(b => new LearnerBookingListItem(
                b.Id,
                b.Status,
                b.BookedAt,
                b.SessionId,
                b.Session.CourseId,
                b.Session.Course.Title,
                b.Session.Course.Subject.Name,
                b.Session.SessionType,
                b.Session.StartsAt,
                b.Session.EndsAt,
                b.Session.Status,
                b.Session.MeetingProvider,
                b.Session.MeetingReference,
                b.Session.Instructors
                    .OrderBy(i => i.Role)
                    .ThenBy(i => i.Id)
                    .Select(i => new SessionInstructorDetail(
                        i.InstructorProfileId,
                        i.InstructorProfile.Membership.User.FirstName,
                        i.InstructorProfile.Membership.User.LastName,
                        i.Role))
                    .ToList()))
            .ToListAsync(cancellationToken);

        return new PagedResult<LearnerBookingListItem>(
            items, page.Page, page.PageSize, totalCount);
    }
}
