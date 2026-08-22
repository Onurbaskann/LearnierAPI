using Learnier.Domain.Common;
using Learnier.Domain.Identity;

namespace Learnier.Domain.Billing;

/// <summary>
/// Kullanicinin bir plan fiyati icin odeme saglayicisina yonlendirildigi checkout oturumu.
/// </summary>
/// <remarks>
/// Bu kayit abonelik degildir. Abonelik ve kredi ancak saglayicidan dogrulanmis basarili
/// odeme olayi geldikten sonra olusturulur.
/// </remarks>
public sealed class CheckoutSession : AggregateRoot, IAuditableEntity, ITenantScoped
{
    private CheckoutSession()
    {
        Provider = string.Empty;
        Currency = string.Empty;
        IdempotencyKey = string.Empty;
    }

    public Guid OrganizationId { get; private set; }

    public Guid UserId { get; private set; }

    public Guid PlanPriceId { get; private set; }

    public Guid? PaymentId { get; private set; }

    public string Provider { get; private set; }

    public string IdempotencyKey { get; private set; }

    public string? ProviderCheckoutSessionId { get; private set; }

    public string? CheckoutUrl { get; private set; }

    public decimal Amount { get; private set; }

    public string Currency { get; private set; }

    public CheckoutSessionStatus Status { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    public DateTimeOffset? CancelledAt { get; private set; }

    public User User { get; private set; } = null!;

    public PlanPrice PlanPrice { get; private set; } = null!;

    public Payment? Payment { get; private set; }

    public DateTimeOffset CreatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    public Guid? UpdatedBy { get; set; }

    public static CheckoutSession Create(
        Guid organizationId,
        Guid userId,
        Guid planPriceId,
        decimal amount,
        string currency,
        string provider,
        string idempotencyKey,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(amount);
        ArgumentException.ThrowIfNullOrWhiteSpace(currency);
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);

        if (organizationId == Guid.Empty || userId == Guid.Empty || planPriceId == Guid.Empty)
        {
            throw new ArgumentException("Kurum, kullanici ve plan fiyati kimlikleri bos olamaz.");
        }

        if (expiresAt <= createdAt)
        {
            throw new ArgumentException("Checkout bitis zamani olusturma zamanindan sonra olmalidir.", nameof(expiresAt));
        }

        return new CheckoutSession
        {
            OrganizationId = organizationId,
            UserId = userId,
            PlanPriceId = planPriceId,
            Amount = amount,
            Currency = currency.Trim().ToUpperInvariant(),
            Provider = provider.Trim().ToLowerInvariant(),
            IdempotencyKey = idempotencyKey.Trim(),
            ExpiresAt = expiresAt,
            Status = CheckoutSessionStatus.Created
        };
    }

    public void MarkReady(string providerCheckoutSessionId, string checkoutUrl)
    {
        EnsureStatus(CheckoutSessionStatus.Created);
        ArgumentException.ThrowIfNullOrWhiteSpace(providerCheckoutSessionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(checkoutUrl);

        if (!Uri.TryCreate(checkoutUrl, UriKind.Absolute, out _))
        {
            throw new ArgumentException("Checkout adresi mutlak bir URL olmalidir.", nameof(checkoutUrl));
        }

        ProviderCheckoutSessionId = providerCheckoutSessionId.Trim();
        CheckoutUrl = checkoutUrl.Trim();
        Status = CheckoutSessionStatus.Ready;
    }

    public void Complete(Guid paymentId, DateTimeOffset completedAt)
    {
        if (Status is not (CheckoutSessionStatus.Created or CheckoutSessionStatus.Ready))
        {
            throw new InvalidOperationException("Yalnizca acik checkout oturumu tamamlanabilir.");
        }

        if (paymentId == Guid.Empty)
        {
            throw new ArgumentException("PaymentId bos olamaz.", nameof(paymentId));
        }

        PaymentId = paymentId;
        CompletedAt = completedAt;
        Status = CheckoutSessionStatus.Completed;
    }

    public void Expire(DateTimeOffset asOf)
    {
        if (asOf < ExpiresAt)
        {
            throw new InvalidOperationException("Suresi dolmamis checkout oturumu expire edilemez.");
        }

        if (Status is CheckoutSessionStatus.Completed)
        {
            throw new InvalidOperationException("Tamamlanmis checkout oturumu expire edilemez.");
        }

        Status = CheckoutSessionStatus.Expired;
    }

    public void Cancel(DateTimeOffset cancelledAt)
    {
        if (Status is CheckoutSessionStatus.Completed)
        {
            throw new InvalidOperationException("Tamamlanmis checkout oturumu iptal edilemez.");
        }

        Status = CheckoutSessionStatus.Cancelled;
        CancelledAt = cancelledAt;
    }

    private void EnsureStatus(CheckoutSessionStatus expected)
    {
        if (Status != expected)
        {
            throw new InvalidOperationException($"Checkout durumu {expected} olmalidir.");
        }
    }
}
