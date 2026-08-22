using Learnier.Domain.Billing;
using Shouldly;

namespace Learnier.UnitTests.Domain;

public sealed class PaymentOrchestrationTests
{
    [Fact]
    public void Checkout_ShouldOnlyCompleteOnceAndKeepPaymentReference()
    {
        var now = DateTimeOffset.UtcNow;
        var checkout = CreateCheckout(now);
        var paymentId = Guid.NewGuid();

        checkout.MarkReady("checkout-42", "https://payment.example/checkout-42");
        checkout.Complete(paymentId, now.AddMinutes(2));

        checkout.Status.ShouldBe(CheckoutSessionStatus.Completed);
        checkout.PaymentId.ShouldBe(paymentId);
        checkout.Provider.ShouldBe("sandbox");
        checkout.Currency.ShouldBe("TRY");
        Should.Throw<InvalidOperationException>(() => checkout.Cancel(now.AddMinutes(3)));
    }

    [Fact]
    public void Checkout_ShouldRejectExpiryBeforeItsDeadline()
    {
        var now = DateTimeOffset.UtcNow;
        var checkout = CreateCheckout(now);

        Should.Throw<InvalidOperationException>(() => checkout.Expire(now.AddMinutes(10)));
        checkout.Status.ShouldBe(CheckoutSessionStatus.Created);
    }

    [Fact]
    public void PaymentAttempt_ShouldSupportProviderActionThenSuccess()
    {
        var attempt = PaymentAttempt.Create(
            Guid.NewGuid(), 1_250m, "try", "Sandbox", "attempt-key");

        attempt.RequireAction("provider-attempt", "https://payment.example/3ds");
        attempt.Status.ShouldBe(PaymentAttemptStatus.RequiresAction);

        var paymentId = Guid.NewGuid();
        attempt.MarkSucceeded(paymentId, "provider-attempt", DateTimeOffset.UtcNow);

        attempt.Status.ShouldBe(PaymentAttemptStatus.Succeeded);
        attempt.PaymentId.ShouldBe(paymentId);
        attempt.FailureCode.ShouldBeNull();
    }

    [Fact]
    public void WebhookInbox_ShouldTrackRetriesWithoutLosingLastFailure()
    {
        var webhook = PaymentWebhookInbox.Receive(
            "Sandbox",
            "event-1",
            "payment.succeeded",
            "{\"id\":\"event-1\"}",
            new string('a', 64),
            DateTimeOffset.UtcNow);

        webhook.StartProcessing();
        webhook.MarkFailed("gecici hata");
        webhook.Status.ShouldBe(WebhookProcessingStatus.Failed);
        webhook.LastError.ShouldBe("gecici hata");

        webhook.StartProcessing();
        webhook.MarkSucceeded(DateTimeOffset.UtcNow);

        webhook.Status.ShouldBe(WebhookProcessingStatus.Succeeded);
        webhook.ProcessingAttemptCount.ShouldBe(2);
        webhook.LastError.ShouldBeNull();
    }

    [Fact]
    public void WebhookInbox_ShouldRejectInvalidPayloadHash()
    {
        Should.Throw<ArgumentException>(() => PaymentWebhookInbox.Receive(
            "sandbox", "event-1", "payment.succeeded", "{}", "short", DateTimeOffset.UtcNow));
    }

    [Fact]
    public void RefundRequest_ShouldTrackRetryAndSuccess()
    {
        var request = RefundRequest.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            500m,
            "Sandbox",
            "refund-key",
            "kullanici talebi");

        request.StartProcessing();
        request.MarkFailed("timeout", "Saglayici cevap vermedi");
        request.Status.ShouldBe(RefundRequestStatus.Failed);

        request.StartProcessing();
        request.MarkSucceeded(DateTimeOffset.UtcNow);

        request.Status.ShouldBe(RefundRequestStatus.Succeeded);
        request.ProcessingAttemptCount.ShouldBe(2);
        request.FailureCode.ShouldBeNull();
        Should.Throw<InvalidOperationException>(() => request.StartProcessing());
    }

    private static CheckoutSession CreateCheckout(DateTimeOffset now)
        => CheckoutSession.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            1_250m,
            "try",
            "Sandbox",
            "checkout-key",
            now,
            now.AddMinutes(30));
}
