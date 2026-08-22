using Learnier.Domain.Common;
using Learnier.Domain.Identity;

namespace Learnier.Domain.Social;

/// <summary>
/// Iki ogrenci arasindaki birebir mesaj.
/// </summary>
/// <remarks>
/// <para>
/// Ayri bir "konusma" varligi bilincli olarak yok. Konusma, iki kullanici
/// cifti tarafindan zaten benzersiz sekilde belirleniyor; ayri tablo yalnizca
/// senkron tutulmasi gereken ikinci bir gercek kaynagi olurdu. Konusma listesi
/// mesajlarin karsi taraf bazinda gruplanmasiyla turetilir.
/// </para>
/// <para>
/// Okundu bilgisi mesaj basinadir (<see cref="ReadAt"/>): okunmamis sayisi
/// boylece tek bir COUNT ile bulunur ve ayri bir sayac alani tutulmaz.
/// </para>
/// </remarks>
public sealed class DirectMessage : Entity, IAuditableEntity
{
    /// <summary>Tek mesajin en fazla karakter sayisi.</summary>
    public const int MaxBodyLength = 2000;

    private DirectMessage()
    {
        Body = string.Empty;
    }

    public Guid SenderUserId { get; private set; }

    public Guid RecipientUserId { get; private set; }

    public string Body { get; private set; }

    /// <summary>UTC gonderim ani. Gosterim katmani kullanicinin saat dilimine cevirir.</summary>
    public DateTimeOffset SentAt { get; private set; }

    /// <summary>Alici mesaji okudugu an; okunmamissa null.</summary>
    public DateTimeOffset? ReadAt { get; private set; }

    public User SenderUser { get; private set; } = null!;

    public User RecipientUser { get; private set; } = null!;

    public DateTimeOffset CreatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    public Guid? UpdatedBy { get; set; }

    public static DirectMessage Send(
        Guid senderUserId,
        Guid recipientUserId,
        string body,
        DateTimeOffset sentAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(body);

        if (senderUserId == recipientUserId)
        {
            throw new ArgumentException(
                "Kullanici kendisine mesaj gonderemez.",
                nameof(recipientUserId));
        }

        var trimmed = body.Trim();
        if (trimmed.Length > MaxBodyLength)
        {
            throw new ArgumentOutOfRangeException(
                nameof(body),
                trimmed.Length,
                $"Mesaj en fazla {MaxBodyLength} karakter olabilir.");
        }

        return new DirectMessage
        {
            SenderUserId = senderUserId,
            RecipientUserId = recipientUserId,
            Body = trimmed,
            SentAt = sentAt
        };
    }

    /// <summary>Okundu isaretlemesi tekrarlanabilir: ilk an korunur.</summary>
    public void MarkRead(DateTimeOffset readAt)
    {
        if (ReadAt is null)
        {
            ReadAt = readAt;
        }
    }
}
