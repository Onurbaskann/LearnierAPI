using Learnier.Application.Common.Abstractions;
using Learnier.Domain.Social;
using Microsoft.EntityFrameworkCore;

namespace Learnier.Infrastructure.Persistence.Repositories;

internal sealed class EfDirectMessageRepository(AppDbContext context) : IDirectMessageRepository
{
    /// <summary>
    /// Konusma listesi: kisi basina yalnizca son mesaj.
    /// </summary>
    /// <remarks>
    /// <c>DISTINCT ON</c> PostgreSQL'e ozgudur ve LINQ ile ifade edilemiyor.
    /// Onceki EF surumu son mesajin metnini bulmak icin kullanicinin tum mesaj
    /// gecmisini cekip bellekte grupluyordu; ekran acikken bu sorgu yoklama
    /// dongusunde tekrarlandigi icin mesaj sayisiyla dogru orantili buyuyordu.
    /// Okunmamis sayisi, <c>(recipient_user_id, read_at)</c> index'ini kullanan
    /// bagimli alt sorgudan gelir ve yalnizca kisi sayisi kadar calisir.
    /// </remarks>
    // Sutun adlari snake_case: model gibi bu sonuc tipi de
    // UseSnakeCaseNamingConvention altinda eslenir.
    private const string ConversationSql = """
        SELECT DISTINCT ON (t.peer_user_id)
               t.peer_user_id                          AS peer_user_id,
               u.email                                 AS email,
               u.first_name                            AS first_name,
               u.last_name                             AS last_name,
               t.body                                  AS last_message_body,
               t.sent_at                               AS last_message_at,
               t.from_me                               AS last_message_from_me,
               (SELECT COUNT(*)::int
                  FROM direct_messages unread
                 WHERE unread.recipient_user_id = {0}
                   AND unread.sender_user_id = t.peer_user_id
                   AND unread.read_at IS NULL)         AS unread_count
          FROM (
                SELECT CASE WHEN m.sender_user_id = {0}
                            THEN m.recipient_user_id
                            ELSE m.sender_user_id
                       END                             AS peer_user_id,
                       m.body,
                       m.sent_at,
                       m.sender_user_id = {0}          AS from_me
                  FROM direct_messages m
                 WHERE m.sender_user_id = {0} OR m.recipient_user_id = {0}
               ) t
          JOIN users u ON u.id = t.peer_user_id
         ORDER BY t.peer_user_id, t.sent_at DESC
        """;

    private sealed record ConversationRow(
        Guid PeerUserId,
        string Email,
        string FirstName,
        string LastName,
        string LastMessageBody,
        DateTimeOffset LastMessageAt,
        bool LastMessageFromMe,
        int UnreadCount);

    public async Task<IReadOnlyList<DirectMessageConversation>> ListConversationsAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var rows = await context.Database
            .SqlQueryRaw<ConversationRow>(ConversationSql, userId)
            .ToListAsync(cancellationToken);

        if (rows.Count == 0)
        {
            return [];
        }

        // Kanali kapanmis kisiler listede kalmamali: arkadaslikten cikarilan biri
        // gorunmeye devam edip acilmaya calisildiginda 403 verirdi. Ders gecmisiyle
        // acilan kanal kapanmadigi icin egitmen yazismalari bundan etkilenmez.
        var reachable = await ReachablePeerIdsAsync(
            userId,
            rows.Select(row => row.PeerUserId).ToList(),
            cancellationToken);

        return rows
            .Where(row => reachable.Contains(row.PeerUserId))
            .Select(row => new DirectMessageConversation(
                row.PeerUserId,
                row.Email,
                row.FirstName,
                row.LastName,
                row.LastMessageBody,
                row.LastMessageAt,
                row.LastMessageFromMe,
                row.UnreadCount))
            .OrderByDescending(conversation => conversation.LastMessageAt)
            .ToList();
    }

    public async Task<IReadOnlyList<DirectMessageItem>> ListThreadAsync(
        Guid userId,
        Guid peerUserId,
        int limit,
        CancellationToken cancellationToken)
    {
        // Limit en yeni uctan alinir, gosterim ise eskiden yeniye siralanir.
        var recent = await ThreadQuery(userId, peerUserId)
            .OrderByDescending(message => message.SentAt)
            .ThenByDescending(message => message.Id)
            .Take(limit)
            .Select(message => new DirectMessageItem(
                message.Id,
                message.SenderUserId,
                message.Body,
                message.SentAt,
                message.ReadAt))
            .ToListAsync(cancellationToken);

        recent.Reverse();
        return recent;
    }

    public async Task<int> CountUnreadAsync(Guid userId, CancellationToken cancellationToken)
        => await context.DirectMessages
            .AsNoTracking()
            .CountAsync(
                message => message.RecipientUserId == userId && message.ReadAt == null,
                cancellationToken);

    public async Task<int> MarkThreadReadAsync(
        Guid userId,
        Guid peerUserId,
        DateTimeOffset readAt,
        CancellationToken cancellationToken)
    {
        var unread = await context.DirectMessages
            .Where(message => message.SenderUserId == peerUserId
                              && message.RecipientUserId == userId
                              && message.ReadAt == null)
            .ToListAsync(cancellationToken);

        foreach (var message in unread)
        {
            message.MarkRead(readAt);
        }

        return unread.Count;
    }

    public async Task<IReadOnlySet<Guid>> ReachablePeerIdsAsync(
        Guid userId,
        IReadOnlyCollection<Guid> peerUserIds,
        CancellationToken cancellationToken)
    {
        if (peerUserIds.Count == 0)
        {
            return new HashSet<Guid>();
        }

        var candidates = peerUserIds.ToList();

        // 1. kapi: kabul edilmis arkadaslik.
        var friends = context.Friendships
            .AsNoTracking()
            .Where(friendship => friendship.Status == FriendshipStatus.Accepted)
            .Where(friendship =>
                (friendship.FirstUserId == userId && candidates.Contains(friendship.SecondUserId))
                || (friendship.SecondUserId == userId && candidates.Contains(friendship.FirstUserId)))
            .Select(friendship => friendship.FirstUserId == userId
                ? friendship.SecondUserId
                : friendship.FirstUserId);

        // 2. kapi: ortak ders. Kullanici ogrenciyse dersini aldigi egitmenler,
        // egitmense ders verdigi ogrenciler erisilebilir sayilir.
        var myInstructors = context.SessionBookings
            .AsNoTracking()
            .Where(booking => booking.LearnerUserId == userId)
            .SelectMany(booking => booking.Session.Instructors
                .Select(instructor => instructor.InstructorProfile.Membership.UserId))
            .Where(instructorUserId => candidates.Contains(instructorUserId));

        var myLearners = context.SessionBookings
            .AsNoTracking()
            .Where(booking => candidates.Contains(booking.LearnerUserId))
            .Where(booking => booking.Session.Instructors.Any(instructor =>
                instructor.InstructorProfile.Membership.UserId == userId))
            .Select(booking => booking.LearnerUserId);

        // Tek gidis donuste UNION olarak calisir.
        var reachable = await friends
            .Union(myInstructors)
            .Union(myLearners)
            .ToListAsync(cancellationToken);

        return reachable.ToHashSet();
    }

    public void Add(DirectMessage message) => context.DirectMessages.Add(message);

    private IQueryable<DirectMessage> ThreadQuery(Guid userId, Guid peerUserId)
        => context.DirectMessages
            .AsNoTracking()
            .Where(message =>
                (message.SenderUserId == userId && message.RecipientUserId == peerUserId)
                || (message.SenderUserId == peerUserId && message.RecipientUserId == userId));
}
