using System.Security.Cryptography;
using System.Text;
using Learnier.Application.Common.Abstractions;
using Learnier.Application.Common.Results;
using Learnier.Domain.Billing;
using Learnier.Domain.Scheduling;

namespace Learnier.Application.Features.Billing.Commands.ProcessPaymentWebhook;

public sealed record ProcessPaymentWebhookResult(
    Guid WebhookInboxId,
    WebhookProcessingStatus Status,
    bool AlreadyProcessed);

public sealed class ProcessPaymentWebhookHandler(
    IPaymentProviderResolver providerResolver,
    IPaymentOrchestrationRepository paymentRepository,
    ISubscriptionPurchaseRepository purchaseRepository,
    IClock clock,
    IUnitOfWork unitOfWork)
{
    public async Task<Result<ProcessPaymentWebhookResult>> Handle(
        string providerName,
        string payload,
        IReadOnlyDictionary<string, string> headers,
        CancellationToken cancellationToken)
    {
        var provider = providerResolver.Find(providerName);
        if (provider is null)
        {
            return PaymentErrors.ProviderNotFound;
        }

        ProviderWebhookEvent providerEvent;
        try
        {
            providerEvent = await provider.VerifyWebhookAsync(
                new ProviderWebhookRequest(payload, headers),
                cancellationToken);
        }
        catch (PaymentProviderVerificationException)
        {
            return PaymentErrors.WebhookInvalid;
        }

        var inbox = await paymentRepository.FindWebhookAsync(
            provider.Name,
            providerEvent.EventId,
            cancellationToken);

        if (inbox is not null
            && inbox.Status is WebhookProcessingStatus.Succeeded or WebhookProcessingStatus.Ignored)
        {
            return new ProcessPaymentWebhookResult(inbox.Id, inbox.Status, true);
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);

        if (inbox is null)
        {
            inbox = PaymentWebhookInbox.Receive(
                provider.Name,
                providerEvent.EventId,
                providerEvent.EventType,
                payload,
                Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload))),
                clock.UtcNow);
            paymentRepository.AddWebhook(inbox);
        }

        inbox.StartProcessing();

        Result processingResult = providerEvent.Kind switch
        {
            ProviderPaymentEventKind.CheckoutCompleted or ProviderPaymentEventKind.PaymentSucceeded
                => await ProcessSuccessfulPayment(provider, providerEvent, cancellationToken),
            ProviderPaymentEventKind.PaymentFailed
                => await ProcessFailedPayment(provider, providerEvent, cancellationToken),
            _ => Result.Success()
        };

        if (processingResult.IsFailure)
        {
            inbox.MarkFailed(processingResult.Error.Code);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return processingResult.Error;
        }

        if (providerEvent.Kind is ProviderPaymentEventKind.CheckoutCompleted
            or ProviderPaymentEventKind.PaymentSucceeded
            or ProviderPaymentEventKind.PaymentFailed)
        {
            inbox.MarkSucceeded(clock.UtcNow);
        }
        else
        {
            inbox.MarkIgnored(clock.UtcNow);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new ProcessPaymentWebhookResult(inbox.Id, inbox.Status, false);
    }

    private async Task<Result> ProcessSuccessfulPayment(
        IPaymentProvider provider,
        ProviderWebhookEvent providerEvent,
        CancellationToken cancellationToken)
    {
        if (providerEvent.ProviderCheckoutSessionId is null
            || providerEvent.ProviderPaymentId is null)
        {
            return PaymentErrors.WebhookMissingCheckout;
        }

        var checkout = await paymentRepository.FindCheckoutByProviderIdAsync(
            provider.Name,
            providerEvent.ProviderCheckoutSessionId,
            cancellationToken);

        if (checkout is null)
        {
            return PaymentErrors.CheckoutNotFound;
        }

        if (checkout.Status is CheckoutSessionStatus.Completed)
        {
            return Result.Success();
        }

        if (providerEvent.Amount != checkout.Amount
            || !string.Equals(providerEvent.Currency, checkout.Currency, StringComparison.OrdinalIgnoreCase))
        {
            return PaymentErrors.AmountMismatch;
        }

        var plan = await purchaseRepository.FindPlanByPriceAsync(
            checkout.PlanPriceId,
            cancellationToken);
        if (plan is null)
        {
            return BillingErrors.PlanPriceNotFound;
        }

        if (await purchaseRepository.HasActiveSubscriptionAsync(
                checkout.UserId, plan.Id, providerEvent.OccurredAt, cancellationToken))
        {
            return BillingErrors.AlreadySubscribed;
        }

        var price = plan.Prices.Single(p => p.Id == checkout.PlanPriceId);
        var periodEnd = price.BillingInterval is BillingInterval.Year
            ? providerEvent.OccurredAt.AddYears(price.BillingIntervalCount)
            : providerEvent.OccurredAt.AddMonths(price.BillingIntervalCount);

        var subscription = Subscription.CreateForUser(
            plan.OrganizationId,
            checkout.UserId,
            price.Id,
            providerEvent.OccurredAt,
            periodEnd);

        if (!string.IsNullOrWhiteSpace(providerEvent.ProviderSubscriptionId))
        {
            subscription.SetProvider(provider.Name, providerEvent.ProviderSubscriptionId);
        }

        subscription.Activate();
        purchaseRepository.AddSubscription(subscription);

        var payment = Payment.Create(
            checkout.Amount,
            checkout.Currency,
            provider.Name,
            subscription.Id,
            checkout.UserId);
        payment.MarkSucceeded(providerEvent.ProviderPaymentId, providerEvent.OccurredAt);
        purchaseRepository.AddPayment(payment);

        foreach (var entitlement in plan.Entitlements)
        {
            if (entitlement.EntitlementType is not EntitlementType.LessonCredit
                || entitlement.Quantity is not { } quantity)
            {
                continue;
            }

            var entitlementEnd = entitlement.ResetPeriod switch
            {
                EntitlementResetPeriod.Week => providerEvent.OccurredAt.AddDays(7),
                EntitlementResetPeriod.Month => providerEvent.OccurredAt.AddMonths(1),
                EntitlementResetPeriod.Year => providerEvent.OccurredAt.AddYears(1),
                _ => periodEnd
            };

            purchaseRepository.AddCredit(CreditLedgerEntry.Grant(
                subscription.Id,
                checkout.UserId,
                entitlement.SessionType,
                quantity,
                providerEvent.OccurredAt,
                entitlementEnd > periodEnd ? periodEnd : entitlementEnd,
                providerEvent.OccurredAt));
        }

        var attempt = PaymentAttempt.Create(
            checkout.Id,
            checkout.Amount,
            checkout.Currency,
            provider.Name,
            $"payment:{providerEvent.ProviderPaymentId}");
        attempt.MarkSucceeded(payment.Id, providerEvent.ProviderPaymentId, providerEvent.OccurredAt);
        paymentRepository.AddAttempt(attempt);

        checkout.Complete(payment.Id, providerEvent.OccurredAt);
        return Result.Success();
    }

    private async Task<Result> ProcessFailedPayment(
        IPaymentProvider provider,
        ProviderWebhookEvent providerEvent,
        CancellationToken cancellationToken)
    {
        if (providerEvent.ProviderCheckoutSessionId is null)
        {
            return PaymentErrors.WebhookMissingCheckout;
        }

        var checkout = await paymentRepository.FindCheckoutByProviderIdAsync(
            provider.Name,
            providerEvent.ProviderCheckoutSessionId,
            cancellationToken);
        if (checkout is null)
        {
            return PaymentErrors.CheckoutNotFound;
        }

        var attempt = PaymentAttempt.Create(
            checkout.Id,
            checkout.Amount,
            checkout.Currency,
            provider.Name,
            $"event:{providerEvent.EventId}");
        attempt.MarkFailed(
            providerEvent.ProviderPaymentId,
            providerEvent.FailureCode,
            providerEvent.FailureMessage,
            providerEvent.OccurredAt);
        paymentRepository.AddAttempt(attempt);

        return Result.Success();
    }
}
