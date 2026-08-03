using Learnier.Domain.Common;
using Learnier.Domain.Identity;
using Learnier.Domain.Scheduling;

namespace Learnier.Domain.Billing;

/// <summary>
/// Ders hakki defterindeki tek bir hareket.
/// </summary>
/// <remarks>
/// <para>
/// Kalan hak <c>SUM(quantity)</c> ile bulunur; hicbir yerde "kalan ders" alani
/// guncellenmez. Kaynak dokumanin 9. bolumunun gerekcesi: sayaci guncellemek
/// iptal, iade ve donem yenileme akislarinda sessizce bozulur, defter ise
/// her hareketi kaydettigi icin her zaman yeniden hesaplanabilir.
/// </para>
/// <para>
/// Sinirsiz grup dersi icin hareket uretilmez - plan erisiminin gecerli olmasi yeterli.
/// </para>
/// </remarks>
public sealed class CreditLedgerEntry : Entity
{
    private CreditLedgerEntry()
    {
    }

    public Guid SubscriptionId { get; private set; }

    public Guid LearnerUserId { get; private set; }

    /// <summary>Hakkin hangi ders turu icin gecerli oldugu.</summary>
    public SessionType SessionType { get; private set; }

    /// <summary>
    /// Hareket miktari. Alacak icin pozitif, harcama icin negatiftir; sifir olamaz.
    /// </summary>
    public int Quantity { get; private set; }

    public CreditTransactionType TransactionType { get; private set; }

    /// <summary>Harcama ve iade hareketlerinde ilgili rezervasyon.</summary>
    public Guid? BookingId { get; private set; }

    /// <summary>Kullanilmazsa hakkin dustugu an.</summary>
    public DateTimeOffset? ExpiresAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public Subscription Subscription { get; private set; } = null!;

    public User Learner { get; private set; } = null!;

    /// <summary>
    /// Donem basinda verilen hakki yazar.
    /// </summary>
    public static CreditLedgerEntry Grant(
        Guid subscriptionId,
        Guid learnerUserId,
        SessionType sessionType,
        int quantity,
        DateTimeOffset createdAt,
        DateTimeOffset? expiresAt = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);

        return new CreditLedgerEntry
        {
            SubscriptionId = subscriptionId,
            LearnerUserId = learnerUserId,
            SessionType = sessionType,
            Quantity = quantity,
            TransactionType = CreditTransactionType.PeriodGrant,
            ExpiresAt = expiresAt,
            CreatedAt = createdAt
        };
    }

    /// <summary>
    /// Rezervasyonda harcanan hakki yazar. Miktar negatife cevrilir.
    /// </summary>
    public static CreditLedgerEntry Consume(
        Guid subscriptionId,
        Guid learnerUserId,
        SessionType sessionType,
        Guid bookingId,
        DateTimeOffset createdAt,
        int quantity = 1)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);

        return new CreditLedgerEntry
        {
            SubscriptionId = subscriptionId,
            LearnerUserId = learnerUserId,
            SessionType = sessionType,
            Quantity = -quantity,
            TransactionType = CreditTransactionType.BookingUsage,
            BookingId = bookingId,
            CreatedAt = createdAt
        };
    }

    /// <summary>
    /// Iptal edilen rezervasyonun hakkini iade eder.
    /// </summary>
    /// <remarks>
    /// Harcama hareketi <b>silinmez veya duzeltilmez</b>; ters yonlu yeni bir hareket
    /// yazilir. Boylece defterde ne olduysa oldugu gibi durur.
    /// </remarks>
    public static CreditLedgerEntry Refund(
        Guid subscriptionId,
        Guid learnerUserId,
        SessionType sessionType,
        Guid bookingId,
        DateTimeOffset createdAt,
        int quantity = 1)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);

        return new CreditLedgerEntry
        {
            SubscriptionId = subscriptionId,
            LearnerUserId = learnerUserId,
            SessionType = sessionType,
            Quantity = quantity,
            TransactionType = CreditTransactionType.CancellationRefund,
            BookingId = bookingId,
            CreatedAt = createdAt
        };
    }

    /// <summary>
    /// Yonetici duzeltmesi. Pozitif veya negatif olabilir, sifir olamaz.
    /// </summary>
    public static CreditLedgerEntry Adjust(
        Guid subscriptionId,
        Guid learnerUserId,
        SessionType sessionType,
        int quantity,
        DateTimeOffset createdAt)
    {
        if (quantity is 0)
        {
            throw new ArgumentException("Duzeltme miktari sifir olamaz.", nameof(quantity));
        }

        return new CreditLedgerEntry
        {
            SubscriptionId = subscriptionId,
            LearnerUserId = learnerUserId,
            SessionType = sessionType,
            Quantity = quantity,
            TransactionType = CreditTransactionType.ManualAdjustment,
            CreatedAt = createdAt
        };
    }

    /// <summary>
    /// Suresi dolan hakki dusen hareket.
    /// </summary>
    public static CreditLedgerEntry Expire(
        Guid subscriptionId,
        Guid learnerUserId,
        SessionType sessionType,
        int quantity,
        DateTimeOffset createdAt)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);

        return new CreditLedgerEntry
        {
            SubscriptionId = subscriptionId,
            LearnerUserId = learnerUserId,
            SessionType = sessionType,
            Quantity = -quantity,
            TransactionType = CreditTransactionType.Expiration,
            CreatedAt = createdAt
        };
    }
}
