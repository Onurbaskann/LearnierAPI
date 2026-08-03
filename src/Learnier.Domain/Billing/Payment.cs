using Learnier.Domain.Common;
using Learnier.Domain.Identity;

namespace Learnier.Domain.Billing;

/// <summary>
/// Alinan odeme kaydi.
/// </summary>
/// <remarks>
/// Ilk surumde cift tarafli muhasebe defteri kurulmuyor; kaynak dokumanin
/// 10. bolumu bunu bilincli olarak kapsam disi birakiyor. Bu tablo saglayicidaki
/// islemin platform tarafindaki izdusumudur.
/// </remarks>
public sealed class Payment : AggregateRoot, IAuditableEntity
{
    private readonly List<Refund> _refunds = [];

    private Payment()
    {
        Currency = string.Empty;
        PaymentProvider = string.Empty;
    }

    /// <summary>Abonelik odemesi degilse (tek seferlik satin alma) bos kalir.</summary>
    public Guid? SubscriptionId { get; private set; }

    public Guid? PayerUserId { get; private set; }

    public decimal Amount { get; private set; }

    public string Currency { get; private set; }

    public PaymentStatus Status { get; private set; }

    public string PaymentProvider { get; private set; }

    public string? ProviderPaymentId { get; private set; }

    public DateTimeOffset? PaidAt { get; private set; }

    public DateTimeOffset? FailedAt { get; private set; }

    public string? FailureReason { get; private set; }

    public Subscription? Subscription { get; private set; }

    public User? Payer { get; private set; }

    public IReadOnlyCollection<Refund> Refunds => _refunds.AsReadOnly();

    public DateTimeOffset CreatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    public Guid? UpdatedBy { get; set; }

    public static Payment Create(
        decimal amount,
        string currency,
        string paymentProvider,
        Guid? subscriptionId = null,
        Guid? payerUserId = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(amount);
        ArgumentException.ThrowIfNullOrWhiteSpace(currency);
        ArgumentException.ThrowIfNullOrWhiteSpace(paymentProvider);

        return new Payment
        {
            Amount = amount,
            Currency = currency.Trim().ToUpperInvariant(),
            PaymentProvider = paymentProvider.Trim().ToLowerInvariant(),
            SubscriptionId = subscriptionId,
            PayerUserId = payerUserId,
            Status = PaymentStatus.Pending
        };
    }

    public void MarkSucceeded(string providerPaymentId, DateTimeOffset paidAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerPaymentId);

        Status = PaymentStatus.Succeeded;
        ProviderPaymentId = providerPaymentId.Trim();
        PaidAt = paidAt;
    }

    public void MarkFailed(DateTimeOffset failedAt, string? reason = null)
    {
        Status = PaymentStatus.Failed;
        FailedAt = failedAt;
        FailureReason = reason?.Trim();
    }

    /// <summary>
    /// Iade kaydi acar ve odemenin durumunu iade toplamina gore gunceller.
    /// </summary>
    public Refund AddRefund(decimal amount, string? reason = null)
    {
        if (Status is not (PaymentStatus.Succeeded or PaymentStatus.PartiallyRefunded))
        {
            throw new InvalidOperationException(
                "Yalnizca basarili bir odeme iade edilebilir.");
        }

        var alreadyRefunded = _refunds
            .Where(r => r.Status is not RefundStatus.Failed)
            .Sum(r => r.Amount);

        if (alreadyRefunded + amount > Amount)
        {
            throw new ArgumentException(
                "Iade toplami odeme tutarini asamaz.",
                nameof(amount));
        }

        var refund = Refund.Create(Id, amount, reason);
        _refunds.Add(refund);

        Status = alreadyRefunded + amount == Amount
            ? PaymentStatus.Refunded
            : PaymentStatus.PartiallyRefunded;

        return refund;
    }
}
