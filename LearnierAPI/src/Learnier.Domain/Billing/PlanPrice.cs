using Learnier.Domain.Common;

namespace Learnier.Domain.Billing;

/// <summary>
/// Planin belirli bir para birimi ve faturalama periyodundaki fiyat surumu.
/// </summary>
/// <remarks>
/// Abonelik plana degil <b>fiyat surumune</b> baglanir. Bu sayede fiyat degistiginde
/// mevcut abonelerin odedigi tutar oldugu gibi kalir ve gecmis raporlar tutarli olur.
/// </remarks>
public sealed class PlanPrice : Entity, IAuditableEntity
{
    private PlanPrice()
    {
        Currency = string.Empty;
    }

    public Guid PlanId { get; private set; }

    /// <summary>ISO 4217 kodu, ornegin <c>TRY</c>.</summary>
    public string Currency { get; private set; }

    public decimal Amount { get; private set; }

    public BillingInterval BillingInterval { get; private set; }

    /// <summary>Kac periyotta bir faturalandigi. 3 + <c>Month</c> = ucer aylik.</summary>
    public int BillingIntervalCount { get; private set; }

    public DateTimeOffset ValidFrom { get; private set; }

    public DateTimeOffset? ValidUntil { get; private set; }

    public PlanPriceStatus Status { get; private set; }

    public SubscriptionPlan Plan { get; private set; } = null!;

    public DateTimeOffset CreatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    public Guid? UpdatedBy { get; set; }

    internal static PlanPrice Create(
        Guid planId,
        string currency,
        decimal amount,
        BillingInterval billingInterval,
        int billingIntervalCount,
        DateTimeOffset validFrom)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(currency);
        ArgumentOutOfRangeException.ThrowIfNegative(amount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(billingIntervalCount);

        return new PlanPrice
        {
            PlanId = planId,
            Currency = currency,
            Amount = amount,
            BillingInterval = billingInterval,
            BillingIntervalCount = billingIntervalCount,
            ValidFrom = validFrom,
            Status = PlanPriceStatus.Active
        };
    }

    internal void Archive(DateTimeOffset validUntil)
    {
        Status = PlanPriceStatus.Archived;
        ValidUntil = validUntil;
    }
}
