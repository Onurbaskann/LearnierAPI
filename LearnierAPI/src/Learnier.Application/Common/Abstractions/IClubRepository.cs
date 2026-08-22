using Learnier.Domain.Social;

namespace Learnier.Application.Common.Abstractions;

public interface IClubRepository
{
    Task<Club?> FindByIdAsync(
        Guid clubId,
        bool includeRooms,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<Club>> ListAsync(
        bool includeInactive,
        CancellationToken cancellationToken);

    Task<ClubRoom?> FindRoomAsync(Guid roomId, CancellationToken cancellationToken);

    Task<IReadOnlyList<ClubMessage>> ListMessagesAsync(
        Guid roomId,
        int limit,
        CancellationToken cancellationToken);

    Task<bool> ExistsForSubjectAsync(
        Guid organizationId,
        Guid subjectId,
        CancellationToken cancellationToken);

    void Add(Club club);

    void AddMessage(ClubMessage message);
}

public interface IClubAccessPolicy
{
    Task<bool> CanAccessSubjectAsync(
        Guid userId,
        Guid subjectId,
        CancellationToken cancellationToken);
}
