using Learnier.Domain.Catalog;
using Learnier.Domain.Common;

namespace Learnier.Domain.Scheduling;

/// <summary>
/// Takvimdeki gercek ders oturumu.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Course"/> egitimin tanimi, bu kayit ise onun belirli bir tarihte
/// gerceklesen ornegidir. Birebir derslerde oturum rezervasyon aninda uretilir;
/// grup derslerinde ise once oturum acilir, ogrenciler sonra rezervasyon yapar.
/// </para>
/// <para>
/// <b>Kontenjan uyarisi:</b> <see cref="HasCapacityFor"/> yalnizca bellege yuklenmis
/// rezervasyonlara bakar. Es zamanli iki istek ayni anda "yer var" gorup kontenjani
/// asabilir. Gercek koruma handler icinde acik islem + oturum satirinin kilitlenmesi
/// ile saglanir; kaynak dokumanin 7. bolumu bunu ozellikle vurguluyor.
/// </para>
/// </remarks>
public sealed class LessonSession : AggregateRoot, IAuditableEntity, ITenantScoped
{
    private readonly List<SessionInstructor> _instructors = [];
    private readonly List<SessionBooking> _bookings = [];

    private LessonSession()
    {
    }

    public Guid OrganizationId { get; private set; }

    public Guid CourseId { get; private set; }

    /// <summary>Sabit kadrolu sinifa ait oturumlarda dolu olur.</summary>
    public Guid? ClassGroupId { get; private set; }

    /// <summary>Oturumda islenen mufredat konusu.</summary>
    public Guid? CourseLessonId { get; private set; }

    public SessionType SessionType { get; private set; }

    /// <summary>UTC baslangic. Gosterim katmani kullanicinin saat dilimine cevirir.</summary>
    public DateTimeOffset StartsAt { get; private set; }

    public DateTimeOffset EndsAt { get; private set; }

    /// <summary>Oturuma ozel kontenjan. Egitimin varsayilanindan farkli olabilir.</summary>
    public int Capacity { get; private set; }

    /// <summary>Oturumun acilmasi icin gereken en az rezervasyon sayisi.</summary>
    public int MinimumParticipants { get; private set; }

    public LessonSessionStatus Status { get; private set; }

    /// <summary>Gorusme saglayicisi: <c>zoom</c>, <c>teams</c>, <c>internal</c>.</summary>
    public string? MeetingProvider { get; private set; }

    /// <summary>Saglayicidaki toplanti kimligi veya baglantisi.</summary>
    public string? MeetingReference { get; private set; }

    public DateTimeOffset? BookingOpensAt { get; private set; }

    public DateTimeOffset? BookingClosesAt { get; private set; }

    /// <summary>Bu ana kadar yapilan iptallerde ders hakki iade edilir.</summary>
    public DateTimeOffset? CancellationDeadlineAt { get; private set; }

    public string? CancellationReason { get; private set; }

    public Course Course { get; private set; } = null!;

    public IReadOnlyCollection<SessionInstructor> Instructors => _instructors.AsReadOnly();

    public IReadOnlyCollection<SessionBooking> Bookings => _bookings.AsReadOnly();

    public DateTimeOffset CreatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    public Guid? UpdatedBy { get; set; }

    public static LessonSession Create(
        Guid organizationId,
        Guid courseId,
        SessionType sessionType,
        DateTimeOffset startsAt,
        DateTimeOffset endsAt,
        int capacity,
        int minimumParticipants,
        Guid? classGroupId = null,
        Guid? courseLessonId = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        ArgumentOutOfRangeException.ThrowIfNegative(minimumParticipants);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(minimumParticipants, capacity);

        if (endsAt <= startsAt)
        {
            throw new ArgumentException("Oturum bitisi baslangictan sonra olmalidir.", nameof(endsAt));
        }

        return new LessonSession
        {
            OrganizationId = organizationId,
            CourseId = courseId,
            SessionType = sessionType,
            StartsAt = startsAt,
            EndsAt = endsAt,
            Capacity = capacity,
            MinimumParticipants = minimumParticipants,
            ClassGroupId = classGroupId,
            CourseLessonId = courseLessonId,
            Status = LessonSessionStatus.Scheduled
        };
    }

    /// <summary>
    /// Rezervasyon penceresini ve ucretsiz iptal sinirini belirler.
    /// </summary>
    public void SetBookingWindow(
        DateTimeOffset? opensAt,
        DateTimeOffset? closesAt,
        DateTimeOffset? cancellationDeadlineAt)
    {
        if (opensAt is not null && closesAt is not null && closesAt < opensAt)
        {
            throw new ArgumentException(
                "Rezervasyon kapanisi acilistan once olamaz.",
                nameof(closesAt));
        }

        BookingOpensAt = opensAt;
        BookingClosesAt = closesAt;
        CancellationDeadlineAt = cancellationDeadlineAt;
    }

    public void SetMeeting(string provider, string reference)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(reference);

        MeetingProvider = provider.Trim().ToLowerInvariant();
        MeetingReference = reference.Trim();
    }

    /// <summary>Verilen anda rezervasyona acik mi.</summary>
    public bool IsBookable(DateTimeOffset now)
        => Status is LessonSessionStatus.Scheduled or LessonSessionStatus.Confirmed
           && (BookingOpensAt is null || now >= BookingOpensAt)
           && (BookingClosesAt is null || now <= BookingClosesAt)
           && now < StartsAt;

    /// <summary>
    /// Yalnizca yuklenmis rezervasyonlara gore yer olup olmadigini soyler.
    /// Es zamanlilik icin sinif aciklamasindaki uyariya bak.
    /// </summary>
    public bool HasCapacityFor(int additionalSeats = 1)
        => ReservedSeatCount + additionalSeats <= Capacity;

    /// <summary>Kontenjani dolduran rezervasyonlar - iptal ve bekleme listesi sayilmaz.</summary>
    public int ReservedSeatCount
        => _bookings.Count(b => b.Status
            is BookingStatus.Reserved
            or BookingStatus.Attended
            or BookingStatus.NoShow);

    public void AssignInstructor(Guid instructorProfileId, SessionInstructorRole role)
    {
        if (_instructors.Exists(i => i.InstructorProfileId == instructorProfileId))
        {
            return;
        }

        _instructors.Add(SessionInstructor.Create(Id, instructorProfileId, role));
    }

    /// <summary>
    /// Ogrenciye yer ayirir. Kontenjan doluysa rezervasyon bekleme listesine alinir.
    /// </summary>
    /// <param name="reservedSeatCount">
    /// Bu rezervasyon oncesindeki dolu koltuk sayisi.
    /// </param>
    /// <remarks>
    /// <para>
    /// Sayimin disaridan verilmesi bilincli. Rezervasyon akisi oturumu satir kilidiyle,
    /// rezervasyonlarini yuklemeden okur; <see cref="ReservedSeatCount"/> orada eksik
    /// kalir ve es zamanli isteklerde kontenjan asilirdi. Cagiran taraf sayimi
    /// veritabanindan, kilit altinda yapmalidir.
    /// </para>
    /// <para>
    /// Ayni ogrencinin ikinci kez rezervasyon yapmasi burada engellenmez; o kontrol
    /// de veritabanina aittir ve <c>session_bookings(session_id, learner_user_id)</c>
    /// UNIQUE kisiti son savunma hatti olarak durur.
    /// </para>
    /// </remarks>
    public SessionBooking Book(
        Guid learnerUserId,
        Guid bookedByUserId,
        BookingAccessSource accessSource,
        DateTimeOffset bookedAt,
        int reservedSeatCount,
        Guid? subscriptionId = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(reservedSeatCount);

        var status = reservedSeatCount < Capacity
            ? BookingStatus.Reserved
            : BookingStatus.Waitlisted;

        var booking = SessionBooking.Create(
            Id,
            learnerUserId,
            bookedByUserId,
            status,
            accessSource,
            bookedAt,
            subscriptionId);

        _bookings.Add(booking);
        return booking;
    }

    /// <summary>
    /// Asgari katilimci sarti saglandiysa oturumu kesinlestirir.
    /// </summary>
    /// <param name="reservedSeatCount">
    /// Dolu koltuk sayisi. Disaridan verilmesi bilincli: rezervasyon akisi oturumu
    /// satir kilidiyle rezervasyonlarini yuklemeden okur, bu yuzden
    /// <see cref="ReservedSeatCount"/> orada eksik kalir ve sarti hep yanlis degerlendirirdi.
    /// Cagiran taraf sayimi veritabanindan yapmalidir.
    /// </param>
    public void Confirm(int reservedSeatCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(reservedSeatCount);

        if (Status is not LessonSessionStatus.Scheduled || reservedSeatCount < MinimumParticipants)
        {
            return;
        }

        Status = LessonSessionStatus.Confirmed;
    }

    public void Cancel(string? reason = null)
    {
        if (Status is LessonSessionStatus.Completed or LessonSessionStatus.Cancelled)
        {
            return;
        }

        Status = LessonSessionStatus.Cancelled;
        CancellationReason = reason?.Trim();
    }

    public void Complete() => Status = LessonSessionStatus.Completed;
}
