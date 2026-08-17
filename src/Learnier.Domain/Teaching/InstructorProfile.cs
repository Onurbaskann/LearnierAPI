using Learnier.Domain.Common;
using Learnier.Domain.Identity;

namespace Learnier.Domain.Teaching;

/// <summary>
/// Egitmenin bir kurumdaki profili.
/// </summary>
/// <remarks>
/// <para>
/// Profil <see cref="User"/> yerine <see cref="OrganizationMembership"/> uzerine kurulur.
/// Kaynak dokumanin 6. bolumunun gerekcesi: ayni kisi iki kurumda egitmen olabilir ve
/// biografisi, saat dilimi, ucreti ve calisma durumu kuruma gore degisir. Profili
/// kullaniciya baglamak bu bilgileri kurumlar arasinda sizdirirdi.
/// </para>
/// <para>
/// Organizasyona uyelik uzerinden ulasildigi icin ayrica <c>OrganizationId</c> tasimaz.
/// </para>
/// </remarks>
public sealed class InstructorProfile : AggregateRoot, IAuditableEntity
{
    private readonly List<InstructorSubject> _subjects = [];
    private readonly List<InstructorAvailability> _availabilities = [];

    private InstructorProfile()
    {
        TimeZoneId = string.Empty;
    }

    public Guid MembershipId { get; private set; }

    public string? Bio { get; private set; }

    public string? Headline { get; private set; }

    public string? Hobbies { get; private set; }

    /// <summary>
    /// Egitmenin uygunluk saatlerinin yorumlandigi saat dilimi.
    /// </summary>
    /// <remarks>
    /// Uygunluk yerel saat olarak girilir; oturum zamanlari UTC saklanir. Donusum icin
    /// bu alan gereklidir ve kurumun saat diliminden bagimsizdir: egitmen baska bir
    /// ulkede yasiyor olabilir.
    /// </remarks>
    public string TimeZoneId { get; private set; }

    public InstructorStatus Status { get; private set; }

    /// <summary>Birebir derslerde varsayilan saatlik ucret.</summary>
    public decimal? DefaultHourlyRate { get; private set; }

    /// <summary>
    /// <see cref="DefaultHourlyRate"/> icin ISO 4217 kodu.
    /// </summary>
    /// <remarks>
    /// Tutar ile para birimi ayrilmaz: ucret girildiyse para birimi de zorunludur.
    /// Bu kural veritabaninda check constraint ile de korunur.
    /// </remarks>
    public string? DefaultHourlyRateCurrency { get; private set; }

    public OrganizationMembership Membership { get; private set; } = null!;

    public IReadOnlyCollection<InstructorSubject> Subjects => _subjects.AsReadOnly();

    public IReadOnlyCollection<InstructorAvailability> Availabilities => _availabilities.AsReadOnly();

    public DateTimeOffset CreatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    public Guid? UpdatedBy { get; set; }

    public static InstructorProfile Create(Guid membershipId, string timeZoneId, string? bio = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(timeZoneId);

        return new InstructorProfile
        {
            MembershipId = membershipId,
            TimeZoneId = timeZoneId,
            Bio = bio?.Trim(),
            Status = InstructorStatus.Pending
        };
    }

    public void Activate() => Status = InstructorStatus.Active;

    public void Suspend() => Status = InstructorStatus.Suspended;

    public void UpdatePublicProfile(string? headline, string? bio, string? hobbies)
    {
        Headline = string.IsNullOrWhiteSpace(headline) ? null : headline.Trim();
        Bio = string.IsNullOrWhiteSpace(bio) ? null : bio.Trim();
        Hobbies = string.IsNullOrWhiteSpace(hobbies) ? null : hobbies.Trim();
    }

    /// <summary>
    /// Saatlik ucreti belirler. Ucret temizlenecekse iki deger de bos gecilir.
    /// </summary>
    public void SetHourlyRate(decimal? rate, string? currency)
    {
        if (rate is null != currency is null)
        {
            throw new ArgumentException(
                "Saatlik ucret ve para birimi birlikte verilmelidir.",
                nameof(currency));
        }

        ArgumentOutOfRangeException.ThrowIfNegative(rate ?? 0m);

        DefaultHourlyRate = rate;
        DefaultHourlyRateCurrency = currency?.Trim().ToUpperInvariant();
    }

    /// <summary>
    /// Egitmene brans yetkinligi ekler. Ayni alan/seviye ikilisi iki kez eklenmez.
    /// </summary>
    public InstructorSubject AddSubject(Guid subjectId, Guid? levelId = null)
    {
        var existing = _subjects.Find(s => s.SubjectId == subjectId && s.LevelId == levelId);
        if (existing is not null)
        {
            existing.Activate();
            return existing;
        }

        var subject = InstructorSubject.Create(Id, subjectId, levelId);
        _subjects.Add(subject);
        return subject;
    }

    /// <summary>
    /// Haftalik uygunluk araligi ekler.
    /// </summary>
    /// <remarks>
    /// Slotlar bu araliklardan uretilir, tek tek kaydedilmez. Boylece egitmen
    /// programini degistirdiginde gelecekteki tum slotlar tutarli kalir.
    /// </remarks>
    public InstructorAvailability AddAvailability(
        DayOfWeek dayOfWeek,
        TimeOnly startLocalTime,
        TimeOnly endLocalTime,
        DateOnly validFrom,
        DateOnly? validUntil = null)
    {
        var availability = InstructorAvailability.Create(
            Id,
            dayOfWeek,
            startLocalTime,
            endLocalTime,
            TimeZoneId,
            validFrom,
            validUntil);

        _availabilities.Add(availability);
        return availability;
    }
}
