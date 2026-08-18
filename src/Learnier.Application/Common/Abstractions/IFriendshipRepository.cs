using Learnier.Domain.Social;

namespace Learnier.Application.Common.Abstractions;

public sealed record FriendshipPeer(
    Guid FriendshipId,
    Guid UserId,
    string Email,
    string FirstName,
    string LastName,
    DateTimeOffset ChangedAt);

public sealed record FriendshipSearchPeer(
    Guid UserId,
    string Email,
    string FirstName,
    string LastName,
    Guid? FriendshipId,
    FriendshipStatus? FriendshipStatus,
    Guid? RequestedByUserId);

public interface IFriendshipRepository
{
    Task<Friendship?> FindBetweenAsync(Guid firstUserId, Guid secondUserId, CancellationToken cancellationToken);
    Task<Friendship?> FindByIdAsync(Guid friendshipId, CancellationToken cancellationToken);
    Task<FriendshipPeer?> FindPeerAsync(Guid friendshipId, Guid currentUserId, CancellationToken cancellationToken);
    Task<IReadOnlyList<FriendshipPeer>> ListFriendsAsync(Guid currentUserId, CancellationToken cancellationToken);
    Task<IReadOnlyList<FriendshipPeer>> ListIncomingRequestsAsync(Guid currentUserId, CancellationToken cancellationToken);
    Task<IReadOnlyList<FriendshipPeer>> ListSentRequestsAsync(Guid currentUserId, CancellationToken cancellationToken);
    Task<IReadOnlyList<FriendshipSearchPeer>> SearchUsersAsync(
        Guid currentUserId,
        string searchTerm,
        int limit,
        CancellationToken cancellationToken);

    /// <summary>
    /// Kullanicinin herhangi bir kurumda ogrenci rolu var mi.
    /// </summary>
    /// <remarks>
    /// Arkadaslik yalnizca ogrenciler arasinda kurulur; egitmen, veli ve yonetici
    /// hesaplari arkadas olarak eklenemez. Arama zaten filtreliyor ama istek
    /// gonderme ucu ham kullanici kimligi aldigi icin kontrol orada da yapilir.
    /// </remarks>
    Task<bool> HasStudentRoleAsync(Guid userId, CancellationToken cancellationToken);
    void Add(Friendship friendship);
    void Remove(Friendship friendship);
}
