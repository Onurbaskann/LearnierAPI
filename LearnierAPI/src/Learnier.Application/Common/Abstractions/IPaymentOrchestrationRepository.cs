using Learnier.Domain.Billing;

namespace Learnier.Application.Common.Abstractions;

/// <summary>Checkout ve webhook islemlerinin kalici odeme orkestrasyonu deposu.</summary>
public interface IPaymentOrchestrationRepository
{
    Task<CheckoutSession?> FindCheckoutAsync(
        Guid checkoutSessionId,
        CancellationToken cancellationToken);

    Task<CheckoutSession?> FindCheckoutByProviderIdAsync(
        string provider,
        string providerCheckoutSessionId,
        CancellationToken cancellationToken);

    Task<CheckoutSession?> FindCheckoutByIdempotencyKeyAsync(
        string provider,
        string idempotencyKey,
        CancellationToken cancellationToken);

    Task<PaymentWebhookInbox?> FindWebhookAsync(
        string provider,
        string providerEventId,
        CancellationToken cancellationToken);

    Task<Payment?> FindPaymentAsync(Guid paymentId, CancellationToken cancellationToken);

    void AddCheckout(CheckoutSession checkoutSession);

    void AddAttempt(PaymentAttempt paymentAttempt);

    void AddWebhook(PaymentWebhookInbox webhook);

    void AddRefundRequest(RefundRequest refundRequest);
}
