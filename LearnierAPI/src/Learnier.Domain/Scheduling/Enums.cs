namespace Learnier.Domain.Scheduling;

/// <summary>
/// Sinifin isleyis bicimi.
/// </summary>
public enum ClassGroupDeliveryType
{
    /// <summary>Sabit ogrenci kadrosu, donem boyunca birlikte ilerler.</summary>
    Cohort,

    /// <summary>Serbest katilim havuzu; ogrenci oturum bazinda katilir.</summary>
    DropInPool
}

public enum ClassGroupStatus
{
    Planned,

    Active,

    Completed,

    Cancelled
}

public enum ClassGroupMemberStatus
{
    Active,

    /// <summary>Kayitli ancak gecici olarak ayrilmis.</summary>
    Paused,

    Left
}

public enum SessionType
{
    Group,

    Private,

    Webinar
}

public enum LessonSessionStatus
{
    /// <summary>Takvime alindi; asgari katilimci sarti henuz saglanmamis olabilir.</summary>
    Scheduled,

    /// <summary>Acilacagi kesinlesti.</summary>
    Confirmed,

    InProgress,

    Completed,

    Cancelled
}

public enum SessionInstructorRole
{
    Lead,

    Assistant,

    Moderator
}

public enum BookingStatus
{
    Reserved,

    /// <summary>Kontenjan dolu; sira <c>BookedAt</c> ile belirlenir.</summary>
    Waitlisted,

    Cancelled,

    Attended,

    NoShow
}

/// <summary>
/// Rezervasyonun hangi hakla yapildigi.
/// </summary>
/// <remarks>
/// Bu alan rezervasyon motoru ile ticari modeli birbirine yapistirmadan baglar:
/// rezervasyon "neyle odendigini" bilir ama abonelik kurallarini bilmez.
/// </remarks>
public enum BookingAccessSource
{
    /// <summary>Abonelik kapsamindaki sinirsiz erisim.</summary>
    Subscription,

    /// <summary>Ders kredisi harcandi; <c>CreditLedgerEntryId</c> dolu olur.</summary>
    Credit,

    /// <summary>Tek seferlik satin alma.</summary>
    DirectPurchase,

    /// <summary>Yonetici tarafindan elle acildi.</summary>
    Admin
}

public enum AttendanceStatus
{
    Present,

    Late,

    /// <summary>Katildi ancak asgari sureyi doldurmadi.</summary>
    Partial,

    Absent
}
