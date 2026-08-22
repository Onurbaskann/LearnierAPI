using Learnier.Application.Common.Abstractions;
using Learnier.Application.Common.Security;
using Learnier.Domain.Identity;
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

    public void Remove(Friendship friendship) => context.Friendships.Remove(friendship);

    public async Task<IReadOnlyList<FriendshipSearchPeer>> SearchUsersAsync(
        Guid currentUserId,
        string searchTerm,
        int limit,
        CancellationToken cancellationToken)
    {
        var pattern = $"%{EscapeLikePattern(searchTerm.Trim())}%";
        var users = await context.Users
            .AsNoTracking()
            .Where(user => user.Id != currentUserId && user.Status == UserStatus.Active)
            // Arkadaslik ogrenciler arasindadir; egitmen ve yonetici hesaplari
            // aramada hic gorunmez. Sunucu tarafi kontrolu icin bkz.
            // SendFriendRequestHandler.
            .Where(user => StudentUserIds().Contains(user.Id))
            .Where(user => EF.Functions.ILike(user.Email, pattern, "\\")
                           || EF.Functions.ILike(user.FirstName, pattern, "\\")
                           || EF.Functions.ILike(user.LastName, pattern, "\\")
                           || EF.Functions.ILike(user.FirstName + " " + user.LastName, pattern, "\\"))
            .OrderBy(user => user.FirstName)
            .ThenBy(user => user.LastName)
            .Take(limit)
            .Select(user => new { user.Id, user.Email, user.FirstName, user.LastName })
            .ToListAsync(cancellationToken);

        if (users.Count == 0)
        {
            return [];
        }

        var userIds = users.Select(user => user.Id).ToList();
        var relations = await context.Friendships
            .AsNoTracking()
            .Where(friendship =>
                (friendship.FirstUserId == currentUserId
                 && userIds.Contains(friendship.SecondUserId))
                || (friendship.SecondUserId == currentUserId
                    && userIds.Contains(friendship.FirstUserId)))
            .ToListAsync(cancellationToken);

        var relationByUserId = relations.ToDictionary(
            friendship => friendship.OtherUserId(currentUserId));

        return users.Select(user =>
        {
            relationByUserId.TryGetValue(user.Id, out var friendship);
            return new FriendshipSearchPeer(
                user.Id,
                user.Email,
                user.FirstName,
                user.LastName,
                friendship?.Id,
                friendship?.Status,
                friendship?.RequestedByUserId);
        }).ToList();
    }

    public async Task<bool> HasStudentRoleAsync(Guid userId, CancellationToken cancellationToken)
        => await StudentUserIds().ContainsAsync(userId, cancellationToken);

    /// <summary>
    /// Askiya alinmamis bir uyelik uzerinden ogrenci rolu tasiyan kullanicilar.
    /// </summary>
    /// <remarks>
    /// Kullanici birden fazla kurumda uye olabilir; herhangi birinde ogrenciyse
    /// yeterli. Ayni kisi hem ogrenci hem egitmen olabilir, o durumda ogrenci
    /// sayilir. <see cref="MembershipRole"/> uzerinde kiraci filtresi tanimli
    /// oldugu icin (bkz. <c>AppDbContext.ApplyDerivedTenantQueryFilters</c>) ve bu
    /// filtre yalnizca sorgu kaynagina degil sonuc kolonuna da bakilmaksizin
    /// uygulandigi icin, filtre acikca <c>IgnoreQueryFilters</c> ile kapatilmazsa
    /// aktif organizasyon disindaki uyelikler sessizce elenirdi.
    /// </remarks>
    private IQueryable<Guid> StudentUserIds()
        => context.MembershipRoles
            .IgnoreQueryFilters([AppDbContext.TenantFilterName])
            .AsNoTracking()
            .Where(membershipRole => membershipRole.Role.Code == SystemRoles.Student)
            .Where(membershipRole => membershipRole.Membership.Status != MembershipStatus.Suspended)
            .Select(membershipRole => membershipRole.Membership.UserId);

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

    private static string EscapeLikePattern(string value)
        => value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);
}
