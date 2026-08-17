using Learnier.Application.Common.Abstractions;
using Learnier.Domain.Scheduling;
using Microsoft.EntityFrameworkCore;

namespace Learnier.Infrastructure.Persistence.Repositories;

/// <inheritdoc cref="ISchedulingRepository"/>
internal sealed class EfSchedulingRepository(AppDbContext context) : ISchedulingRepository
{
    public async Task<ClassGroup?> FindClassGroupAsync(
        Guid classGroupId,
        bool includeMembers,
        CancellationToken cancellationToken)
    {
        var query = context.ClassGroups.AsQueryable();

        if (includeMembers)
        {
            query = query.Include(g => g.Members);
        }

        return await query.FirstOrDefaultAsync(g => g.Id == classGroupId, cancellationToken);
    }

    public async Task<LessonSession?> FindSessionAsync(
        Guid sessionId,
        bool includeInstructors,
        CancellationToken cancellationToken)
    {
        var query = context.LessonSessions.AsQueryable();

        if (includeInstructors)
        {
            query = query.Include(s => s.Instructors);
        }

        return await query.FirstOrDefaultAsync(s => s.Id == sessionId, cancellationToken);
    }

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// <c>FOR UPDATE</c> ham SQL ile veriliyor: EF Core'un LINQ yuzeyinde satir
    /// kilidi icin bir karsiligi yok.
    /// </para>
    /// <para>
    /// Kiraci filtresi ham sorguda otomatik uygulanmaz; bu yuzden kilitlenen satir
    /// bulunduktan sonra <b>ayrica</b> filtreli sorguyla dogrulanir. Aksi halde
    /// baska bir kurumun oturumu kilitlenip okunabilirdi.
    /// </para>
    /// </remarks>
    public async Task<LessonSession?> FindSessionForUpdateAsync(
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        var locked = await context.LessonSessions
            .FromSqlInterpolated(
                $"SELECT * FROM lesson_sessions WHERE id = {sessionId} FOR UPDATE")
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(cancellationToken);

        if (locked is null)
        {
            return null;
        }

        // Kilit alindi; simdi kiraci sinirini normal (filtreli) sorguyla dogrula.
        var visible = await context.LessonSessions
            .AnyAsync(s => s.Id == sessionId, cancellationToken);

        return visible ? locked : null;
    }

    public async Task<int> CountReservedSeatsAsync(Guid sessionId, CancellationToken cancellationToken)
        => await context.SessionBookings
            .CountAsync(
                b => b.SessionId == sessionId
                     && (b.Status == BookingStatus.Reserved
                         || b.Status == BookingStatus.Attended
                         || b.Status == BookingStatus.NoShow),
                cancellationToken);

    public async Task<SessionBooking?> FindBookingAsync(
        Guid bookingId,
        CancellationToken cancellationToken)
        // Rezervasyon kendi organizasyon sutununu tasimaz; kiraci siniri oturum
        // uzerinden korunur, oturum ise filtreye tabi.
        => await context.SessionBookings
            .Where(b => b.Id == bookingId)
            .Where(b => context.LessonSessions.Any(s => s.Id == b.SessionId))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<SessionBooking?> FindActiveBookingAsync(
        Guid sessionId,
        Guid learnerUserId,
        CancellationToken cancellationToken)
        => await context.SessionBookings
            .FirstOrDefaultAsync(
                b => b.SessionId == sessionId
                     && b.LearnerUserId == learnerUserId
                     && b.Status != BookingStatus.Cancelled,
                cancellationToken);

    public async Task<SessionBooking?> FindNextWaitlistedAsync(
        Guid sessionId,
        CancellationToken cancellationToken)
        => await context.SessionBookings
            .Where(b => b.SessionId == sessionId && b.Status == BookingStatus.Waitlisted)
            // Sira rezervasyon zamanina gore; esitlikte kimlik ile kararli hale gelir.
            .OrderBy(b => b.BookedAt)
            .ThenBy(b => b.Id)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<SessionBooking>> ListActiveBookingsAsync(
        Guid sessionId,
        CancellationToken cancellationToken)
        => await context.SessionBookings
            .Include(booking => booking.Attendance)
            .Where(b => b.SessionId == sessionId && b.Status != BookingStatus.Cancelled)
            .ToListAsync(cancellationToken);

    /// <inheritdoc />
    /// <remarks>
    /// Cakisma esitlik degil aralik kesisimi sorusudur: yeni oturum mevcut oturum
    /// bitmeden basliyorsa ve mevcut oturum yeni oturum bitmeden basliyorsa cakisir.
    /// Bitisi digerinin baslangicina esit olan oturumlar cakisma sayilmaz.
    /// </remarks>
    public async Task<bool> HasInstructorConflictAsync(
        Guid instructorProfileId,
        DateTimeOffset startsAt,
        DateTimeOffset endsAt,
        Guid? excludeSessionId,
        CancellationToken cancellationToken)
    {
        var query = context.SessionInstructors
            .Where(si => si.InstructorProfileId == instructorProfileId);

        if (excludeSessionId is { } excludeId)
        {
            query = query.Where(si => si.SessionId != excludeId);
        }

        return await query.AnyAsync(
            si => context.LessonSessions.Any(
                s => s.Id == si.SessionId
                     && s.Status != LessonSessionStatus.Cancelled
                     && startsAt < s.EndsAt
                     && endsAt > s.StartsAt),
            cancellationToken);
    }

    public async Task<bool> InstructorExistsAsync(
        Guid instructorProfileId,
        CancellationToken cancellationToken)
        => await context.InstructorProfiles
            .Where(p => p.Id == instructorProfileId)
            .AnyAsync(p => context.Memberships.Any(m => m.Id == p.MembershipId), cancellationToken);

    public async Task<bool> LockInstructorAsync(
        Guid instructorProfileId,
        CancellationToken cancellationToken)
    {
        var locked = await context.InstructorProfiles
            .FromSqlInterpolated(
                $"SELECT * FROM instructor_profiles WHERE id = {instructorProfileId} FOR UPDATE")
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(cancellationToken);

        return locked is not null
               && await context.Memberships.AnyAsync(
                   m => m.Id == locked.MembershipId,
                   cancellationToken);
    }

    public async Task<int> CountActiveMembersAsync(Guid classGroupId, CancellationToken cancellationToken)
        => await context.ClassGroupMembers
            .CountAsync(
                m => m.ClassGroupId == classGroupId
                     && m.Status == ClassGroupMemberStatus.Active,
                cancellationToken);

    public void AddClassGroup(ClassGroup classGroup) => context.ClassGroups.Add(classGroup);

    public void AddSession(LessonSession session) => context.LessonSessions.Add(session);

    public void AddBooking(SessionBooking booking) => context.SessionBookings.Add(booking);

    public void AddAttendance(SessionAttendance attendance) => context.SessionAttendances.Add(attendance);
}
