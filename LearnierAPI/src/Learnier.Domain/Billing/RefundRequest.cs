using Learnier.Domain.Common;
using Learnier.Domain.Identity;

namespace Learnier.Domain.Billing;

/// <summary>Bir iadenin saglayiciya gonderilme ve tekrar denenme sureci.</summary>
public sealed class RefundRequest : Entity, IAuditableEntity
{
    private RefundRequest()
    {
        Provider = string.Empty;
        IdempotencyKey = string.Empty;
    }

    public Guid PaymentId { get; private set; }

    public Guid RefundId { get; private set; }

    public Guid RequestedByUserId { get; private set; }

    public decimal Amount { get; private set; }

    public string Provider { get; private set; }

    public string IdempotencyKey { get; private set; }

    public string? Reason { get; private set; }

    public RefundRequestStatus Status { get; private set; }

    public int ProcessingAttemptCount { get; private set; }

    public string? FailureCode { get; private set; }

    public string? FailureMessage { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    public Payment Payment { get; private set; } = null!;

    public Refund Refund { get; private set; } = null!;

    public User RequestedByUser { get; private set; } = null!;

    public DateTimeOffset CreatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    public Guid? UpdatedBy { get; set; }

    public static RefundRequest Create(
        Guid paymentId,
        Guid refundId,
        Guid requestedByUserId,
        decimal amount,
        string provider,
        string idempotencyKey,
        string? reason = null)
    {
        if (paymentId == Guid.Empty || refundId == Guid.Empty || requestedByUserId == Guid.Empty)
        {
            throw new ArgumentException("Odeme, iade ve talep eden kullanici kimlikleri bos olamaz.");
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(amount);
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);

        return new RefundRequest
        {
            PaymentId = paymentId,
            RefundId = refundId,
            RequestedByUserId = requestedByUserId,
            Amount = amount,
            Provider = provider.Trim().ToLowerInvariant(),
            IdempotencyKey = idempotencyKey.Trim(),
            Reason = reason?.Trim(),
            Status = RefundRequestStatus.Pending
        };
    }

    public void StartProcessing()
    {
        if (Status is RefundRequestStatus.Succeeded or RefundRequestStatus.Cancelled)
        {
            throw new InvalidOperationException("Sonuclanmis iade talebi yeniden islenemez.");
        }

        ProcessingAttemptCount++;
        FailureCode = null;
        FailureMessage = null;
        Status = RefundRequestStatus.Processing;
    }

    public void MarkSucceeded(DateTimeOffset completedAt)
    {
        EnsureProcessing();
        CompletedAt = completedAt;
        Status = RefundRequestStatus.Succeeded;
    }

    public void MarkFailed(string? failureCode, string? failureMessage)
    {
        EnsureProcessing();
        FailureCode = failureCode?.Trim();
        FailureMessage = failureMessage?.Trim();
        Status = RefundRequestStatus.Failed;
    }

    public void Cancel(DateTimeOffset completedAt)
    {
        if (Status is RefundRequestStatus.Succeeded)
        {
            throw new InvalidOperationException("Basarili iade talebi iptal edilemez.");
        }

        CompletedAt = completedAt;
        Status = RefundRequestStatus.Cancelled;
    }

    private void EnsureProcessing()
    {
        if (Status is not RefundRequestStatus.Processing)
        {
            throw new InvalidOperationException("Iade talebi isleniyor durumunda olmalidir.");
        }
    }
}
