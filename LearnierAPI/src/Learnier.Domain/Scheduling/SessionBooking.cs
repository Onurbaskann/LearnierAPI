using Learnier.Domain.Billing;
using Learnier.Domain.Common;
using Learnier.Domain.Identity;

namespace Learnier.Domain.Scheduling;

/// <summary>
/// Ogrencinin bir oturuma rezervasyonu.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="LearnerUserId"/> ile <see cref="BookedByUserId"/> ayridir: veli veya
/// yonetici baskasi adina rezervasyon yapabilir. Ikisini tek alanda birlestirmek
/// "kimin dersi" ile "kimin islemi" bilgisini kaybettirirdi.
/// </para>
/// <para>
/// Bekleme listesi icin ayri tablo kurulmadi; <see cref="BookingStatus.Waitlisted"/>
/// durumu ve <see cref="BookedAt"/> siralamasi ilk surum icin yeterli.
/// </para>
/// </remarks>
public sealed class SessionBooking : Entity, IAuditableEntity
{
    private SessionBooking()
    {
    }

    public Guid SessionId { get; private set; }

    public Guid LearnerUserId { get; private set; }

    /// <summary>Islemi yapan kullanici: ogrencinin kendisi, velisi veya yonetici.</summary>
    public Guid BookedByUserId { get; private set; }

    public BookingStatus Status { get; private set; }

    public BookingAccessSource AccessSource { get; private set; }

    /// <summary>Rezervasyonun dayandigi abonelik.</summary>
    public Guid? SubscriptionId { get; private set; }

    /// <summary>
    /// Harcanan ders kredisi hareketi.
    /// </summary>
    /// <remarks>
    /// Kredi ile yapilan rezervasyonlarda dolar. Iade edildiginde bu kayit
    /// degistirilmez; ledger'a ters yonlu yeni bir hareket yazilir.
    /// </remarks>
    public Guid? CreditLedgerEntryId { get; private set; }

    public DateTimeOffset BookedAt { get; private set; }

    public DateTimeOffset? CancelledAt { get; private set; }

    public string? CancellationReason { get; private set; }

    public LessonSession Session { get; private set; } = null!;

    public User Learner { get; private set; } = null!;

    public Subscription? Subscription { get; private set; }

    public SessionAttendance? Attendance { get; private set; }

    public DateTimeOffset CreatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    public Guid? UpdatedBy { get; set; }

    internal static SessionBooking Create(
        Guid sessionId,
        Guid learnerUserId,
        Guid bookedByUserId,
        BookingStatus status,
        BookingAccessSource accessSource,
        DateTimeOffset bookedAt,
        Guid? subscriptionId)
        => new()
        {
            SessionId = sessionId,
            LearnerUserId = learnerUserId,
            BookedByUserId = bookedByUserId,
            Status = status,
            AccessSource = accessSource,
            BookedAt = bookedAt,
            SubscriptionId = subscriptionId
        };

    /// <summary>
    /// Harcanan kredi hareketini rezervasyona baglar.
    /// </summary>
    public void AttachCreditEntry(Guid creditLedgerEntryId)
        => CreditLedgerEntryId = creditLedgerEntryId;

    public void Cancel(DateTimeOffset cancelledAt, string? reason = null)
    {
        if (Status is BookingStatus.Cancelled)
        {
            return;
        }

        Status = BookingStatus.Cancelled;
        CancelledAt = cancelledAt;
        CancellationReason = reason?.Trim();
    }

    /// <summary>
    /// Bekleme listesindeki rezervasyonu bosalan kontenjana alir.
    /// </summary>
    public void Promote()
    {
        if (Status is BookingStatus.Waitlisted)
        {
            Status = BookingStatus.Reserved;
        }
    }

    public void MarkAttended() => Status = BookingStatus.Attended;

    public void MarkNoShow() => Status = BookingStatus.NoShow;
}
