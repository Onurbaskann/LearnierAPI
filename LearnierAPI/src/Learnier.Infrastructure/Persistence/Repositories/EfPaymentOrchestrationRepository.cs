using Learnier.Application.Common.Abstractions;
using Learnier.Domain.Billing;
using Microsoft.EntityFrameworkCore;

namespace Learnier.Infrastructure.Persistence.Repositories;

internal sealed class EfPaymentOrchestrationRepository(AppDbContext context)
    : IPaymentOrchestrationRepository
{
    public Task<CheckoutSession?> FindCheckoutAsync(
        Guid checkoutSessionId,
        CancellationToken cancellationToken)
        => context.CheckoutSessions
            .Include(c => c.Payment)
            .SingleOrDefaultAsync(c => c.Id == checkoutSessionId, cancellationToken);

    public Task<CheckoutSession?> FindCheckoutByProviderIdAsync(
        string provider,
        string providerCheckoutSessionId,
        CancellationToken cancellationToken)
        => context.CheckoutSessions
            .Include(c => c.Payment)
            .SingleOrDefaultAsync(
            c => c.Provider == provider
                 && c.ProviderCheckoutSessionId == providerCheckoutSessionId,
            cancellationToken);

    public Task<CheckoutSession?> FindCheckoutByIdempotencyKeyAsync(
        string provider,
        string idempotencyKey,
        CancellationToken cancellationToken)
        => context.CheckoutSessions
            .Include(c => c.Payment)
            .SingleOrDefaultAsync(
                c => c.Provider == provider && c.IdempotencyKey == idempotencyKey,
                cancellationToken);

    public Task<PaymentWebhookInbox?> FindWebhookAsync(
        string provider,
        string providerEventId,
        CancellationToken cancellationToken)
        => context.PaymentWebhookInbox.SingleOrDefaultAsync(
            w => w.Provider == provider && w.ProviderEventId == providerEventId,
            cancellationToken);

    public Task<Payment?> FindPaymentAsync(Guid paymentId, CancellationToken cancellationToken)
        => context.Payments
            .Include(p => p.Refunds)
            .SingleOrDefaultAsync(p => p.Id == paymentId, cancellationToken);

    public void AddCheckout(CheckoutSession checkoutSession)
        => context.CheckoutSessions.Add(checkoutSession);

    public void AddAttempt(PaymentAttempt paymentAttempt)
        => context.PaymentAttempts.Add(paymentAttempt);

    public void AddWebhook(PaymentWebhookInbox webhook)
        => context.PaymentWebhookInbox.Add(webhook);

    public void AddRefundRequest(RefundRequest refundRequest)
        => context.RefundRequests.Add(refundRequest);
}
