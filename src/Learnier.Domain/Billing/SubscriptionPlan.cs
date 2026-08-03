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
