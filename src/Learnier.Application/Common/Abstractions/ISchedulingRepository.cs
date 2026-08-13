using Learnier.Domain.Scheduling;

namespace Learnier.Application.Common.Abstractions;

/// <summary>
/// Planlama ve rezervasyon yazma islemleri.
/// </summary>
public interface ISchedulingRepository
{
    Task<ClassGroup?> FindClassGroupAsync(Guid classGroupId, bool includeMembers, CancellationToken cancellationToken);

    Task<LessonSession?> FindSessionAsync(Guid sessionId, bool includeInstructors, CancellationToken cancellationToken);

    /// <summary>
    /// Oturumu satir kilidiyle getirir.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>SELECT ... FOR UPDATE</c> uygular: ayni oturuma es zamanli gelen ikinci
    /// istek, ilk islem bitene kadar bekler. Kontenjan asimini engelleyen asil
    /// mekanizma budur; bellekteki sayim tek basina yeterli degildir.
    /// </para>
    /// <para>
    /// Yalnizca acik bir islem icinde cagrilmalidir - kilit islem sonunda birakilir.
    /// </para>
    /// </remarks>
    Task<LessonSession?> FindSessionForUpdateAsync(Guid sessionId, CancellationToken cancellationToken);

    /// <summary>
    /// Kontenjani dolduran rezervasyon sayisi: iptal ve bekleme listesi haric.
    /// </summary>
    Task<int> CountReservedSeatsAsync(Guid sessionId, CancellationToken cancellationToken);

    Task<SessionBooking?> FindBookingAsync(Guid bookingId, CancellationToken cancellationToken);

    Task<SessionBooking?> FindActiveBookingAsync(
        Guid sessionId,
        Guid learnerUserId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Bekleme listesindeki en eski rezervasyon.
    /// </summary>
    /// <remarks>
    /// Sira <c>BookedAt</c> ile belirlenir; kaynak dokuman ayri bir bekleme listesi
    /// tablosu yerine bu yaklasimi oneriyor.
    /// </remarks>
    Task<SessionBooking?> FindNextWaitlistedAsync(Guid sessionId, CancellationToken cancellationToken);

    /// <summary>Oturum iptalinde kapatilacak rezervasyonlar.</summary>
    Task<IReadOnlyList<SessionBooking>> ListActiveBookingsAsync(
        Guid sessionId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Egitmenin verilen aralikta baska bir oturumu var mi?
    /// </summary>
    Task<bool> HasInstructorConflictAsync(
        Guid instructorProfileId,
        DateTimeOffset startsAt,
        DateTimeOffset endsAt,
        Guid? excludeSessionId,
        CancellationToken cancellationToken);

    Task<bool> InstructorExistsAsync(Guid instructorProfileId, CancellationToken cancellationToken);

    /// <summary>
    /// Es zamanli manuel slot acma islemlerini siraya koymak icin egitmen profilini kilitler.
    /// </summary>
    Task<bool> LockInstructorAsync(Guid instructorProfileId, CancellationToken cancellationToken);

    Task<int> CountActiveMembersAsync(Guid classGroupId, CancellationToken cancellationToken);

    void AddClassGroup(ClassGroup classGroup);

    void AddSession(LessonSession session);

    void AddBooking(SessionBooking booking);

    void AddAttendance(SessionAttendance attendance);
}
