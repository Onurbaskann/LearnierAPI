using Learnier.Domain.Common;

namespace Learnier.Domain.Billing;

/// <summary>Bir checkout oturumunda saglayiciya yapilan tek bir odeme denemesi.</summary>
public sealed class PaymentAttempt : Entity, IAuditableEntity
{
    private PaymentAttempt()
    {
        Provider = string.Empty;
        Currency = string.Empty;
        IdempotencyKey = string.Empty;
    }

    public Guid CheckoutSessionId { get; private set; }

    public Guid? PaymentId { get; private set; }

    public string Provider { get; private set; }

    public string IdempotencyKey { get; private set; }

    public string? ProviderPaymentAttemptId { get; private set; }

    public decimal Amount { get; private set; }

    public string Currency { get; private set; }

    public PaymentAttemptStatus Status { get; private set; }

    public string? NextActionUrl { get; private set; }

    public string? FailureCode { get; private set; }

    public string? FailureMessage { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    public CheckoutSession CheckoutSession { get; private set; } = null!;

    public Payment? Payment { get; private set; }

    public DateTimeOffset CreatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    public Guid? UpdatedBy { get; set; }

    public static PaymentAttempt Create(
        Guid checkoutSessionId,
        decimal amount,
        string currency,
        string provider,
        string idempotencyKey)
    {
        if (checkoutSessionId == Guid.Empty)
        {
            throw new ArgumentException("CheckoutSessionId bos olamaz.", nameof(checkoutSessionId));
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(amount);
        ArgumentException.ThrowIfNullOrWhiteSpace(currency);
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);

        return new PaymentAttempt
        {
            CheckoutSessionId = checkoutSessionId,
            Amount = amount,
            Currency = currency.Trim().ToUpperInvariant(),
            Provider = provider.Trim().ToLowerInvariant(),
            IdempotencyKey = idempotencyKey.Trim(),
            Status = PaymentAttemptStatus.Pending
        };
    }

    public void RequireAction(string providerPaymentAttemptId, string nextActionUrl)
    {
        EnsurePending();
        SetProviderAttempt(providerPaymentAttemptId);
        ArgumentException.ThrowIfNullOrWhiteSpace(nextActionUrl);

        if (!Uri.TryCreate(nextActionUrl, UriKind.Absolute, out _))
        {
            throw new ArgumentException("Sonraki islem adresi mutlak bir URL olmalidir.", nameof(nextActionUrl));
        }

        NextActionUrl = nextActionUrl.Trim();
        Status = PaymentAttemptStatus.RequiresAction;
    }

    public void MarkSucceeded(
        Guid paymentId,
        string providerPaymentAttemptId,
        DateTimeOffset completedAt)
    {
        if (Status is PaymentAttemptStatus.Succeeded or PaymentAttemptStatus.Cancelled)
        {
            throw new InvalidOperationException("Tamamlanmis odeme denemesi yeniden sonuclandirilemez.");
        }

        if (paymentId == Guid.Empty)
        {
            throw new ArgumentException("PaymentId bos olamaz.", nameof(paymentId));
        }

        SetProviderAttempt(providerPaymentAttemptId);
        PaymentId = paymentId;
        CompletedAt = completedAt;
        FailureCode = null;
        FailureMessage = null;
        Status = PaymentAttemptStatus.Succeeded;
    }

    public void MarkFailed(
        string? providerPaymentAttemptId,
        string? failureCode,
        string? failureMessage,
        DateTimeOffset completedAt)
    {
        if (Status is PaymentAttemptStatus.Succeeded or PaymentAttemptStatus.Cancelled)
        {
            throw new InvalidOperationException("Tamamlanmis odeme denemesi yeniden sonuclandirilemez.");
        }

        if (!string.IsNullOrWhiteSpace(providerPaymentAttemptId))
        {
            SetProviderAttempt(providerPaymentAttemptId);
        }

        FailureCode = failureCode?.Trim();
        FailureMessage = failureMessage?.Trim();
        CompletedAt = completedAt;
        Status = PaymentAttemptStatus.Failed;
    }

    public void Cancel(DateTimeOffset completedAt)
    {
        if (Status is PaymentAttemptStatus.Succeeded)
        {
            throw new InvalidOperationException("Basarili odeme denemesi iptal edilemez.");
        }

        CompletedAt = completedAt;
        Status = PaymentAttemptStatus.Cancelled;
    }

    private void EnsurePending()
    {
        if (Status is not PaymentAttemptStatus.Pending)
        {
            throw new InvalidOperationException("Odeme denemesi beklemede olmalidir.");
        }
    }

    private void SetProviderAttempt(string providerPaymentAttemptId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerPaymentAttemptId);
        ProviderPaymentAttemptId = providerPaymentAttemptId.Trim();
    }
}
