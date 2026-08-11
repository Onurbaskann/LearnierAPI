using Learnier.Domain.Social;

namespace Learnier.Application.Common.Abstractions;

public sealed record FriendshipPeer(
    Guid FriendshipId,
    Guid UserId,
    string Email,
    string FirstName,
    string LastName,
    DateTimeOffset ChangedAt);

public interface IFriendshipRepository
{
    Task<Friendship?> FindBetweenAsync(Guid firstUserId, Guid secondUserId, CancellationToken cancellationToken);
    Task<Friendship?> FindByIdAsync(Guid friendshipId, CancellationToken cancellationToken);
    Task<FriendshipPeer?> FindPeerAsync(Guid friendshipId, Guid currentUserId, CancellationToken cancellationToken);
    Task<IReadOnlyList<FriendshipPeer>> ListFriendsAsync(Guid currentUserId, CancellationToken cancellationToken);
    Task<IReadOnlyList<FriendshipPeer>> ListIncomingRequestsAsync(Guid currentUserId, CancellationToken cancellationToken);
    Task<IReadOnlyList<FriendshipPeer>> ListSentRequestsAsync(Guid currentUserId, CancellationToken cancellationToken);
    void Add(Friendship friendship);
}
