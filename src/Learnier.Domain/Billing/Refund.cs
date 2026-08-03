using Learnier.Domain.Common;

namespace Learnier.Domain.Billing;

/// <summary>
/// Bir odemenin tamamina veya bir kismina yapilan iade.
/// </summary>
/// <remarks>
/// Iade odeme satirini degistirmez, ayri kayit olarak yazilir. Ayni gerekce
/// <c>CreditLedgerEntry</c> icin de gecerli: gecmis duzeltilmez, uzerine yazilir.
/// </remarks>
public sealed class Refund : Entity, IAuditableEntity
{
    private Refund()
    {
    }

    public Guid PaymentId { get; private set; }

    public decimal Amount { get; private set; }

    public string? Reason { get; private set; }

    public RefundStatus Status { get; private set; }

    public string? ProviderRefundId { get; private set; }

    public Payment Payment { get; private set; } = null!;

    public DateTimeOffset CreatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    public Guid? UpdatedBy { get; set; }

    internal static Refund Create(Guid paymentId, decimal amount, string? reason)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(amount);

        return new Refund
        {
            PaymentId = paymentId,
            Amount = amount,
            Reason = reason?.Trim(),
            Status = RefundStatus.Pending
        };
    }

    public void MarkSucceeded(string providerRefundId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerRefundId);

        Status = RefundStatus.Succeeded;
        ProviderRefundId = providerRefundId.Trim();
    }

    public void MarkFailed() => Status = RefundStatus.Failed;
}
