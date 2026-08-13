using Learnier.Domain.Common;

namespace Learnier.Domain.Billing;

/// <summary>
/// Satisa sunulan abonelik plani - "Ingilizce Premium", "Yazilim Akademisi" gibi.
/// </summary>
/// <remarks>
/// Plan fiyat icermez: fiyatlar <see cref="PlanPrice"/> icinde versiyonlanir.
/// Boylece fiyat degistiginde eski aboneliklerin gecmisi bozulmaz.
/// </remarks>
public sealed class SubscriptionPlan : AggregateRoot, IAuditableEntity, ITenantScoped
{
    private readonly List<PlanPrice> _prices = [];
    private readonly List<PlanEntitlement> _entitlements = [];

    private SubscriptionPlan()
    {
        Name = string.Empty;
    }

    public Guid OrganizationId { get; private set; }

    public string Name { get; private set; }

    public string? Description { get; private set; }

    public CatalogAccess CatalogAccess { get; private set; }

    public PlanStatus Status { get; private set; }

    /// <summary>Her faturalama ayinda verilecek birebir ders hakki.</summary>
    public int? MonthlyLessonCredits { get; private set; }

    /// <summary>Paketin izin verdigi ders suresi: 30 veya 50 dakika.</summary>
    public int? LessonDurationMinutes { get; private set; }

    public bool IsLessonPackage
        => MonthlyLessonCredits is not null && LessonDurationMinutes is not null;

    public IReadOnlyCollection<PlanPrice> Prices => _prices.AsReadOnly();

    public IReadOnlyCollection<PlanEntitlement> Entitlements => _entitlements.AsReadOnly();

    public DateTimeOffset CreatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    public Guid? UpdatedBy { get; set; }

    public static SubscriptionPlan Create(
        Guid organizationId,
        string name,
        CatalogAccess catalogAccess,
        string? description = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return new SubscriptionPlan
        {
            OrganizationId = organizationId,
            Name = name.Trim(),
            Description = description?.Trim(),
            CatalogAccess = catalogAccess,
            Status = PlanStatus.Draft
        };
    }

    public static SubscriptionPlan CreateLessonPackage(
        Guid organizationId,
        string name,
        int monthlyLessonCredits,
        int lessonDurationMinutes,
        string? description = null)
    {
        var plan = Create(
            organizationId,
            name,
            CatalogAccess.Restricted,
            description);

        plan.ConfigureLessonPackage(monthlyLessonCredits, lessonDurationMinutes);

        return plan;
    }

    /// <summary>
    /// Eski bir plani ders paketi bilgileriyle bir defaya mahsus zenginlestirir.
    /// Tanimlanan kosullar degistirilemez; farkli kosullar yeni plan gerektirir.
    /// </summary>
    public void ConfigureLessonPackage(int monthlyLessonCredits, int lessonDurationMinutes)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(monthlyLessonCredits);

        if (lessonDurationMinutes is not (30 or 50))
        {
            throw new ArgumentOutOfRangeException(
                nameof(lessonDurationMinutes),
                lessonDurationMinutes,
                "Ders suresi yalnizca 30 veya 50 dakika olabilir.");
        }

        if (MonthlyLessonCredits is not null || LessonDurationMinutes is not null)
        {
            if (MonthlyLessonCredits == monthlyLessonCredits
                && LessonDurationMinutes == lessonDurationMinutes)
            {
                return;
            }

            throw new InvalidOperationException(
                "Ders paketi kosullari degistirilemez; yeni plan olusturulmalidir.");
        }

        MonthlyLessonCredits = monthlyLessonCredits;
        LessonDurationMinutes = lessonDurationMinutes;

        var monthlyPrivateCredit = _entitlements.Find(entitlement =>
            entitlement.EntitlementType == EntitlementType.LessonCredit
            && entitlement.SessionType == Scheduling.SessionType.Private);

        if (monthlyPrivateCredit is null)
        {
            AddEntitlement(
                EntitlementType.LessonCredit,
                Scheduling.SessionType.Private,
                monthlyLessonCredits,
                EntitlementResetPeriod.Month);
        }
        else if (monthlyPrivateCredit.Quantity != monthlyLessonCredits
            || monthlyPrivateCredit.ResetPeriod != EntitlementResetPeriod.Month)
        {
            throw new InvalidOperationException(
                "Mevcut ders hakki aylik paket kosullariyla uyumlu degildir.");
        }
    }

    /// <summary>
    /// Yeni fiyat surumu ekler ve ayni para birimindeki onceki fiyati kapatir.
    /// </summary>
    /// <remarks>
    /// Mevcut fiyat kaydi <b>guncellenmez</b>. Kaynak dokumanin 8. bolumunun gerekcesi:
    /// eski aboneliklerin hangi tutardan satildigi izlenebilir kalmali.
    /// </remarks>
    public PlanPrice AddPrice(
        string currency,
        decimal amount,
        BillingInterval billingInterval,
        int billingIntervalCount,
        DateTimeOffset validFrom)
    {
        var normalizedCurrency = currency.Trim().ToUpperInvariant();

        var current = _prices.Find(p =>
            p.Status is PlanPriceStatus.Active
            && p.Currency == normalizedCurrency
            && p.BillingInterval == billingInterval
            && p.BillingIntervalCount == billingIntervalCount);

        current?.Archive(validFrom);

        var price = PlanPrice.Create(
            Id,
            normalizedCurrency,
            amount,
            billingInterval,
            billingIntervalCount,
            validFrom);

        _prices.Add(price);
        return price;
    }

    /// <summary>
    /// Plana hak tanimi ekler - "haftada 3 birebir ders" veya "sinirsiz grup dersi" gibi.
    /// </summary>
    public PlanEntitlement AddEntitlement(
        EntitlementType entitlementType,
        Scheduling.SessionType sessionType,
        int? quantity,
        EntitlementResetPeriod resetPeriod)
    {
        var entitlement = PlanEntitlement.Create(Id, entitlementType, sessionType, quantity, resetPeriod);
        _entitlements.Add(entitlement);
        return entitlement;
    }

    public void Activate() => Status = PlanStatus.Active;

    public void Retire() => Status = PlanStatus.Retired;
}
