using Learnier.Application.Common.Abstractions;
using Learnier.Domain.Social;
using Microsoft.EntityFrameworkCore;

namespace Learnier.Infrastructure.Persistence.Repositories;

internal sealed class EfFriendshipRepository(AppDbContext context) : IFriendshipRepository
{
    public async Task<Friendship?> FindBetweenAsync(
        Guid firstUserId,
        Guid secondUserId,
        CancellationToken cancellationToken)
    {
        var (first, second) = OrderPair(firstUserId, secondUserId);
        return await context.Friendships.FirstOrDefaultAsync(
            friendship => friendship.FirstUserId == first && friendship.SecondUserId == second,
            cancellationToken);
    }

    public async Task<Friendship?> FindByIdAsync(
        Guid friendshipId,
        CancellationToken cancellationToken)
        => await context.Friendships.FirstOrDefaultAsync(
            friendship => friendship.Id == friendshipId,
            cancellationToken);

    public async Task<FriendshipPeer?> FindPeerAsync(
        Guid friendshipId,
        Guid currentUserId,
        CancellationToken cancellationToken)
        => await ProjectPeers(
                context.Friendships.Where(friendship => friendship.Id == friendshipId),
                currentUserId)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<FriendshipPeer>> ListFriendsAsync(
        Guid currentUserId,
        CancellationToken cancellationToken)
    {
        var peers = await ProjectPeers(
            context.Friendships
                .AsNoTracking()
                .Where(friendship => friendship.Status == FriendshipStatus.Accepted)
                .Where(friendship => friendship.FirstUserId == currentUserId
                                     || friendship.SecondUserId == currentUserId),
            currentUserId).ToListAsync(cancellationToken);

        return peers
            .OrderBy(peer => peer.FirstName)
            .ThenBy(peer => peer.LastName)
            .ToList();
    }

    public async Task<IReadOnlyList<FriendshipPeer>> ListIncomingRequestsAsync(
        Guid currentUserId,
        CancellationToken cancellationToken)
    {
        var peers = await ProjectPeers(
            context.Friendships
                .AsNoTracking()
                .Where(friendship => friendship.Status == FriendshipStatus.Pending)
                .Where(friendship => friendship.RequestedByUserId != currentUserId)
                .Where(friendship => friendship.FirstUserId == currentUserId
                                     || friendship.SecondUserId == currentUserId),
            currentUserId).ToListAsync(cancellationToken);

        return peers
            .OrderBy(peer => peer.ChangedAt)
            .ToList();
    }

    public async Task<IReadOnlyList<FriendshipPeer>> ListSentRequestsAsync(
        Guid currentUserId,
        CancellationToken cancellationToken)
    {
        var peers = await ProjectPeers(
            context.Friendships
                .AsNoTracking()
                .Where(friendship => friendship.Status == FriendshipStatus.Pending
                                     && friendship.RequestedByUserId == currentUserId),
            currentUserId).ToListAsync(cancellationToken);

        return peers
            .OrderBy(peer => peer.ChangedAt)
            .ToList();
    }

    public void Add(Friendship friendship) => context.Friendships.Add(friendship);

    private static IQueryable<FriendshipPeer> ProjectPeers(
        IQueryable<Friendship> query,
        Guid currentUserId)
        => query.Select(friendship => new FriendshipPeer(
            friendship.Id,
            friendship.FirstUserId == currentUserId
                ? friendship.SecondUserId
                : friendship.FirstUserId,
            friendship.FirstUserId == currentUserId
                ? friendship.SecondUser.Email
                : friendship.FirstUser.Email,
            friendship.FirstUserId == currentUserId
                ? friendship.SecondUser.FirstName
                : friendship.FirstUser.FirstName,
            friendship.FirstUserId == currentUserId
                ? friendship.SecondUser.LastName
                : friendship.FirstUser.LastName,
            friendship.RespondedAt ?? friendship.RequestedAt));

    private static (Guid First, Guid Second) OrderPair(Guid left, Guid right)
        => left.CompareTo(right) < 0 ? (left, right) : (right, left);
}
