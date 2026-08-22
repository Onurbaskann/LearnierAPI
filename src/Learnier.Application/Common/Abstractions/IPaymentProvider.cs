namespace Learnier.Application.Common.Abstractions;

/// <summary>
/// Stripe, iyzico veya baska bir odeme sistemini uygulama katmanindan ayiran adaptor sozlesmesi.
/// </summary>
public interface IPaymentProvider
{
    string Name { get; }

    TimeSpan CheckoutLifetime { get; }

    Task<ProviderCheckoutResult> CreateCheckoutAsync(
        ProviderCheckoutRequest request,
        CancellationToken cancellationToken);

    /// <summary>Imzayi dogrular ve saglayici olayini ortak formata cevirir.</summary>
    Task<ProviderWebhookEvent> VerifyWebhookAsync(
        ProviderWebhookRequest request,
        CancellationToken cancellationToken);

    Task<ProviderRefundResult> CreateRefundAsync(
        ProviderRefundRequest request,
        CancellationToken cancellationToken);
}

public sealed record ProviderCheckoutRequest(
    Guid CheckoutSessionId,
    Guid UserId,
    Guid PlanPriceId,
    decimal Amount,
    string Currency,
    string CustomerEmail,
    DateTimeOffset ExpiresAt,
    string IdempotencyKey);

public sealed record ProviderCheckoutResult(
    string ProviderCheckoutSessionId,
    string CheckoutUrl,
    DateTimeOffset ExpiresAt);

public sealed record ProviderWebhookRequest(
    string Payload,
    IReadOnlyDictionary<string, string> Headers);

public sealed record ProviderWebhookEvent(
    string EventId,
    string EventType,
    ProviderPaymentEventKind Kind,
    string? ProviderCheckoutSessionId,
    string? ProviderPaymentId,
    string? ProviderSubscriptionId,
    decimal? Amount,
    string? Currency,
    DateTimeOffset OccurredAt,
    string? FailureCode = null,
    string? FailureMessage = null);

public sealed class PaymentProviderVerificationException(string message) : Exception(message);

public enum ProviderPaymentEventKind
{
    CheckoutCompleted,
    PaymentSucceeded,
    PaymentFailed,
    SubscriptionRenewed,
    SubscriptionPastDue,
    SubscriptionCancelled,
    RefundSucceeded,
    RefundFailed,
    Unknown
}

public sealed record ProviderRefundRequest(
    Guid RefundRequestId,
    string ProviderPaymentId,
    decimal Amount,
    string Currency,
    string? Reason,
    string IdempotencyKey);

public sealed record ProviderRefundResult(string ProviderRefundId);
