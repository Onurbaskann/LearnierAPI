using Learnier.Domain.Common;

namespace Learnier.Domain.Scheduling;

/// <summary>
/// Rezervasyonun gerceklesen katilim kaydi.
/// </summary>
/// <remarks>
/// Rezervasyon ile birebir iliskilidir ve ondan ayri tutulur: rezervasyon
/// "yer ayirtildi" bilgisidir, bu kayit ise dersin fiilen nasil gectigi.
/// Ders tamamlanmadan bu satir olusmaz.
/// </remarks>
public sealed class SessionAttendance : Entity, IAuditableEntity
{
    private SessionAttendance()
    {
    }

    public Guid BookingId { get; private set; }

    public DateTimeOffset? JoinedAt { get; private set; }

    public DateTimeOffset? LeftAt { get; private set; }

    /// <summary>Derste gecirilen toplam dakika.</summary>
    public int AttendedMinutes { get; private set; }

    public AttendanceStatus Status { get; private set; }

    /// <summary>Yoklamayi isaretleyen kullanici. Otomatik isaretlemede bos kalir.</summary>
    public Guid? MarkedByUserId { get; private set; }

    public SessionBooking Booking { get; private set; } = null!;

    public DateTimeOffset CreatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    public Guid? UpdatedBy { get; set; }

    public static SessionAttendance Create(
        Guid bookingId,
        AttendanceStatus status,
        int attendedMinutes,
        DateTimeOffset? joinedAt = null,
        DateTimeOffset? leftAt = null,
        Guid? markedByUserId = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(attendedMinutes);

        if (joinedAt is not null && leftAt is not null && leftAt < joinedAt)
        {
            throw new ArgumentException("Ayrilma zamani katilimdan once olamaz.", nameof(leftAt));
        }

        return new SessionAttendance
        {
            BookingId = bookingId,
            Status = status,
            AttendedMinutes = attendedMinutes,
            JoinedAt = joinedAt,
            LeftAt = leftAt,
            MarkedByUserId = markedByUserId
        };
    }
}
