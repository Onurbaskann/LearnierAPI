using Learnier.Application.Common.Models;
using Learnier.Application.Features.Scheduling.Queries;
using Learnier.Domain.Scheduling;

namespace Learnier.Application.Common.Abstractions;

/// <summary>Planlama ekranlari icin salt okunur projeksiyonlar.</summary>
public interface ISchedulingQueries
{
    Task<PagedResult<ClassGroupListItem>> ListClassGroupsAsync(
        PageRequest page,
        Guid? courseId,
        ClassGroupStatus? status,
        CancellationToken cancellationToken);

    Task<ClassGroupDetail?> FindClassGroupDetailAsync(
        Guid classGroupId,
        CancellationToken cancellationToken);

    Task<PagedResult<SessionListItem>> ListSessionsAsync(
        PageRequest page,
        Guid? courseId,
        DateTimeOffset? from,
        DateTimeOffset? until,
        LessonSessionStatus? status,
        CancellationToken cancellationToken);

    Task<SessionDetail?> FindSessionDetailAsync(
        Guid sessionId,
        CancellationToken cancellationToken);

    /// <param name="onlyBookable">
    /// Dogruysa rezervasyon penceresi kapanmis slotlar listeye alinmaz. Ogrenciye
    /// donen listede dogru, egitmenin kendi takviminde yanlis kullanilir.
    /// </param>
    Task<IReadOnlyList<InstructorSlotListItem>> ListInstructorSlotsAsync(
        Guid instructorProfileId,
        Guid? courseId,
        DateTimeOffset from,
        DateTimeOffset until,
        DateTimeOffset now,
        bool onlyBookable,
        CancellationToken cancellationToken);

    Task<PagedResult<LearnerBookingListItem>> ListLearnerBookingsAsync(
        PageRequest page,
        Guid learnerUserId,
        DateTimeOffset? from,
        DateTimeOffset? until,
        BookingStatus? status,
        DateTimeOffset now,
        CancellationToken cancellationToken);
}
