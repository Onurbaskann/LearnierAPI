using Learnier.Domain.Common;

namespace Learnier.Domain.Teaching;

/// <summary>
/// Egitmenin haftalik tekrar eden uygunluk araligi.
/// </summary>
/// <remarks>
/// <para>
/// Kaynak dokumanin 6. bolumu bilincli bir sadelestirme yapiyor: RRULE veya JSON
/// recurrence saklanmiyor. Haftalik aralik + tarihli istisna, ihtiyacin buyuk
/// cogunlugunu karsiliyor ve sorgulanmasi cok daha ucuz.
/// </para>
/// <para>
/// <see cref="ValidFrom"/> / <see cref="ValidUntil"/> programin gecmisini korur:
/// egitmen programini degistirdiginde eski kayit guncellenmez, kapatilir ve yeni
/// kayit acilir. Boylece gecmis oturumlarin hangi programa gore olustugu kaybolmaz.
/// </para>
/// </remarks>
public sealed class InstructorAvailability : Entity, IAuditableEntity
{
    private InstructorAvailability()
    {
        TimeZoneId = string.Empty;
    }

    public Guid InstructorProfileId { get; private set; }

    public DayOfWeek DayOfWeek { get; private set; }

    public TimeOnly StartLocalTime { get; private set; }

    public TimeOnly EndLocalTime { get; private set; }

    /// <summary>
    /// Yerel saatlerin yorumlandigi saat dilimi.
    /// </summary>
    /// <remarks>
    /// Profildeki degerin kopyasidir. Egitmen saat dilimini degistirdiginde gecmis
    /// araliklarin anlami kaymasin diye kayit aninda sabitlenir.
    /// </remarks>
    public string TimeZoneId { get; private set; }

    public DateOnly ValidFrom { get; private set; }

    /// <summary>Bos ise arali suresiz gecerlidir.</summary>
    public DateOnly? ValidUntil { get; private set; }

    public InstructorProfile InstructorProfile { get; private set; } = null!;

    public DateTimeOffset CreatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    public Guid? UpdatedBy { get; set; }

    internal static InstructorAvailability Create(
        Guid instructorProfileId,
        DayOfWeek dayOfWeek,
        TimeOnly startLocalTime,
        TimeOnly endLocalTime,
        string timeZoneId,
        DateOnly validFrom,
        DateOnly? validUntil)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(timeZoneId);

        if (endLocalTime <= startLocalTime)
        {
            throw new ArgumentException(
                "Uygunluk bitisi baslangictan sonra olmalidir.",
                nameof(endLocalTime));
        }

        if (validUntil is not null && validUntil < validFrom)
        {
            throw new ArgumentException(
                "Gecerlilik bitisi baslangictan once olamaz.",
                nameof(validUntil));
        }

        return new InstructorAvailability
        {
            InstructorProfileId = instructorProfileId,
            DayOfWeek = dayOfWeek,
            StartLocalTime = startLocalTime,
            EndLocalTime = endLocalTime,
            TimeZoneId = timeZoneId,
            ValidFrom = validFrom,
            ValidUntil = validUntil
        };
    }

    /// <summary>
    /// Araligi belirtilen tarihten itibaren kapatir. Kayit silinmez ki gecmis korunsun.
    /// </summary>
    public void Close(DateOnly validUntil) => ValidUntil = validUntil;
}
