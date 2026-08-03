namespace Learnier.Domain.Teaching;

public enum InstructorStatus
{
    /// <summary>Basvurmus, henuz onaylanmamis.</summary>
    Pending,

    Active,

    /// <summary>Gecici olarak ders vermiyor; mevcut oturumlari korunur.</summary>
    Inactive,

    Suspended
}

public enum InstructorSubjectStatus
{
    Active,

    Inactive
}

/// <summary>
/// Haftalik uygunlugu belirli bir tarihte degistiren istisnanin yonu.
/// </summary>
public enum AvailabilityOverrideType
{
    /// <summary>O gun (veya verilen saat araligi) uygun degil - izin, tatil.</summary>
    Unavailable,

    /// <summary>Haftalik programda olmayan ek uygunluk.</summary>
    Available
}
