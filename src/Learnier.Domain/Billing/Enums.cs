namespace Learnier.Domain.Billing;

public enum PlanStatus
{
    Draft,

    Active,

    /// <summary>Yeni abonelige kapali; mevcut abonelikler devam eder.</summary>
    Retired
}

/// <summary>
/// Planin katalogu ne kadar kapsadigi.
/// </summary>
public enum CatalogAccess
{
    /// <summary>Kurumun tum katalogu. Ayrica erisim satiri yazilmasi gerekmez.</summary>
    All,

    /// <summary>Yalnizca <c>PlanSubjectAccess</c> / <c>PlanCourseAccess</c> ile verilenler.</summary>
    Restricted
}

public enum BillingInterval
{
    Month,

    Year
}

public enum PlanPriceStatus
{
    Active,

    /// <summary>Yeni satista kullanilmaz; mevcut aboneliklerin gecmisi icin korunur.</summary>
    Archived
}

/// <summary>
/// Planin abonesine verdigi hak turu.
/// </summary>
public enum EntitlementType
{
    /// <summary>Kontenjan varsa sinirsiz rezervasyon. Ledger hareketi uretilmez.</summary>
    BookingAccess,

    /// <summary>Sayili ders hakki. Her donem ledger'a alacak olarak yazilir.</summary>
    LessonCredit
}

/// <summary>
/// Ders hakkinin hangi sikilikta yenilendigi.
/// </summary>
/// <remarks>
/// <see cref="Week"/> kaynak dokumanda yoktu; urunun paketleri "haftada 3 ders"
/// seklinde satildigi icin eklendi.
/// </remarks>
public enum EntitlementResetPeriod
{
    /// <summary>Yenilenmez; abonelik boyunca bir kez verilir.</summary>
    Subscription,

    Week,

    Month,

    Year
}

public enum SubscriptionStatus
{
    /// <summary>Odeme bekleniyor.</summary>
    Pending,

    Active,

    /// <summary>Odeme alinamadi; erisim gecici olarak kisitli.</summary>
    PastDue,

    Cancelled,

    Expired
}

/// <summary>
/// Ders hakki defterindeki hareket turu.
/// </summary>
/// <remarks>
/// Kalan hak bu hareketlerin toplamidir. Dogrudan bir "kalan ders" alani tutulmaz;
/// kaynak dokumanin 9. bolumu iptal, iade ve donem yenilemede o alanin bozuldugunu
/// belirtiyor.
/// </remarks>
public enum CreditTransactionType
{
    /// <summary>Donem basinda verilen hak.</summary>
    PeriodGrant,

    /// <summary>Rezervasyonda harcanan hak.</summary>
    BookingUsage,

    /// <summary>Suresi icinde iptal edilen rezervasyonun iadesi.</summary>
    CancellationRefund,

    /// <summary>Yonetici duzeltmesi.</summary>
    ManualAdjustment,

    /// <summary>Kullanilmadan suresi dolan hak.</summary>
    Expiration
}

public enum PaymentStatus
{
    Pending,

    Succeeded,

    Failed,

    Refunded,

    PartiallyRefunded
}

public enum RefundStatus
{
    Pending,

    Succeeded,

    Failed
}
