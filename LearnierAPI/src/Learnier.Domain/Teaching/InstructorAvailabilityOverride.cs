using Learnier.Domain.Common;

namespace Learnier.Domain.Teaching;

/// <summary>
/// Belirli bir tarihte haftalik uygunlugu degistiren istisna: izin, tatil veya ek mesai.
/// </summary>
/// <remarks>
/// <para>
/// Kaynak dokumanda bu yapinin adi <c>instructor_availability_exceptions</c>. Kod
/// tarafinda "Exception" ile biten tur adi hata turlerine ayrildigindan
/// <c>Override</c> adi kullanildi; kavram aynidir.
/// </para>
/// <para>
/// Saat alanlari bos birakildiginda istisna gun boyunu kapsar. Ikisi birlikte
/// verildiginde yalnizca o aralik etkilenir - ornegin "Sali ogleden sonra izinli".
/// </para>
/// </remarks>
public sealed class InstructorAvailabilityOverride : Entity, IAuditableEntity
{
    private InstructorAvailabilityOverride()
    {
    }

    public Guid InstructorProfileId { get; private set; }

    public DateOnly OverrideDate { get; private set; }

    /// <summary>Bos ise istisna gun boyunca gecerlidir.</summary>
    public TimeOnly? StartLocalTime { get; private set; }

    public TimeOnly? EndLocalTime { get; private set; }

    public AvailabilityOverrideType OverrideType { get; private set; }

    public string? Reason { get; private set; }

    public InstructorProfile InstructorProfile { get; private set; } = null!;

    public DateTimeOffset CreatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    public Guid? UpdatedBy { get; set; }

    public static InstructorAvailabilityOverride Create(
        Guid instructorProfileId,
        DateOnly overrideDate,
        AvailabilityOverrideType overrideType,
        TimeOnly? startLocalTime = null,
        TimeOnly? endLocalTime = null,
        string? reason = null)
    {
        if (startLocalTime is null != endLocalTime is null)
        {
            throw new ArgumentException(
                "Istisna saatleri ya birlikte verilmeli ya da ikisi de bos birakilmalidir.",
                nameof(endLocalTime));
        }

        if (startLocalTime is not null && endLocalTime <= startLocalTime)
        {
            throw new ArgumentException(
                "Istisna bitisi baslangictan sonra olmalidir.",
                nameof(endLocalTime));
        }

        return new InstructorAvailabilityOverride
        {
            InstructorProfileId = instructorProfileId,
            OverrideDate = overrideDate,
            OverrideType = overrideType,
            StartLocalTime = startLocalTime,
            EndLocalTime = endLocalTime,
            Reason = reason?.Trim()
        };
    }
}
