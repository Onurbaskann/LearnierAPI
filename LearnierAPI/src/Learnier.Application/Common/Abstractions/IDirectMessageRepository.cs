using Learnier.Domain.Social;

namespace Learnier.Application.Common.Abstractions;

/// <summary>Konusma listesindeki tek satir: karsi taraf ve son mesajin ozeti.</summary>
public sealed record DirectMessageConversation(
    Guid PeerUserId,
    string Email,
    string FirstName,
    string LastName,
    string LastMessageBody,
    DateTimeOffset LastMessageAt,
    bool LastMessageFromMe,
    int UnreadCount);

public sealed record DirectMessageItem(
    Guid MessageId,
    Guid SenderUserId,
    string Body,
    DateTimeOffset SentAt,
    DateTimeOffset? ReadAt);

public interface IDirectMessageRepository
{
    /// <summary>Kullanicinin tum konusmalari, en son mesaji en ustte olacak sekilde.</summary>
    Task<IReadOnlyList<DirectMessageConversation>> ListConversationsAsync(
        Guid userId,
        CancellationToken cancellationToken);

    /// <summary>Iki kullanici arasindaki mesajlar, eskiden yeniye.</summary>
    Task<IReadOnlyList<DirectMessageItem>> ListThreadAsync(
        Guid userId,
        Guid peerUserId,
        int limit,
        CancellationToken cancellationToken);

    /// <summary>Rozet icin: kullanicinin okunmamis mesaj sayisi.</summary>
    Task<int> CountUnreadAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Karsi taraftan gelen okunmamislari okundu isaretler, sayisini doner.</summary>
    Task<int> MarkThreadReadAsync(
        Guid userId,
        Guid peerUserId,
        DateTimeOffset readAt,
        CancellationToken cancellationToken);

    /// <summary>
    /// Verilen kisiler arasindan yazisma kanali acik olanlari dondurur.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Kanal iki kapidan biriyle acilir: kabul edilmis arkadaslik ya da ortak
    /// ders gecmisi (taraflardan biri ogrenci olarak rezervasyon yapmis, digeri
    /// o oturumun egitmeni). Rezervasyonun durumu aranmaz — bir kez ders alinmis
    /// olmasi kanali kalici olarak acar.
    /// </para>
    /// <para>
    /// Kural tek yerde tutulur: hem konusma listesi filtresi hem mesaj gonderme
    /// izni bu sorgudan beslenir, boylece ikisi birbirinden ayrisamaz.
    /// </para>
    /// </remarks>
    Task<IReadOnlySet<Guid>> ReachablePeerIdsAsync(
        Guid userId,
        IReadOnlyCollection<Guid> peerUserIds,
        CancellationToken cancellationToken);

    void Add(DirectMessage message);
}
