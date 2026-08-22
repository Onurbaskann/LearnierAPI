using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Learnier.Application.Common.Abstractions;
using Learnier.Domain.Billing;
using Microsoft.Extensions.Options;

namespace Learnier.Infrastructure.Billing;

/// <summary>
/// Gelistirme ve entegrasyon testlerinde gercek para hareketi olmadan checkout/webhook
/// hattini calistiran saglayici. Uretim adaptoru ayni <see cref="IPaymentProvider"/>
/// sozlesmesini uygular.
/// </summary>
public sealed class SandboxPaymentProvider(IOptions<PaymentOptions> options) : IPaymentProvider
{
    public const string ProviderName = "sandbox";
    public const string SignatureHeader = "X-Sandbox-Signature";

    public string Name => ProviderName;

    public TimeSpan CheckoutLifetime
        => TimeSpan.FromMinutes(options.Value.CheckoutLifetimeMinutes);

    public Task<ProviderCheckoutResult> CreateCheckoutAsync(
        ProviderCheckoutRequest request,
        CancellationToken cancellationToken)
    {
        var providerCheckoutId = $"sandbox-checkout-{request.CheckoutSessionId:N}";
        var baseUrl = options.Value.PublicApiBaseUrl.TrimEnd('/');
        var checkoutUrl = string.Create(
            CultureInfo.InvariantCulture,
            $"{baseUrl}/api/v1/payments/sandbox/checkouts/{request.CheckoutSessionId}/complete");

        return Task.FromResult(new ProviderCheckoutResult(
            providerCheckoutId,
            checkoutUrl,
            request.ExpiresAt));
    }

    public Task<ProviderWebhookEvent> VerifyWebhookAsync(
        ProviderWebhookRequest request,
        CancellationToken cancellationToken)
    {
        if (!request.Headers.TryGetValue(SignatureHeader, out var suppliedSignature))
        {
            throw new PaymentProviderVerificationException("Sandbox webhook imzasi eksik.");
        }

        var expectedSignature = Sign(request.Payload);
        var suppliedBytes = Encoding.UTF8.GetBytes(suppliedSignature.ToLowerInvariant());
        var expectedBytes = Encoding.UTF8.GetBytes(expectedSignature);
        if (suppliedBytes.Length != expectedBytes.Length
            || !CryptographicOperations.FixedTimeEquals(suppliedBytes, expectedBytes))
        {
            throw new PaymentProviderVerificationException("Sandbox webhook imzasi gecersiz.");
        }

        SandboxWebhookPayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<SandboxWebhookPayload>(
                request.Payload,
                JsonOptions);
        }
        catch (JsonException exception)
        {
            throw new PaymentProviderVerificationException(exception.Message);
        }

        if (payload is null
            || string.IsNullOrWhiteSpace(payload.EventId)
            || string.IsNullOrWhiteSpace(payload.EventType))
        {
            throw new PaymentProviderVerificationException("Sandbox webhook govdesi gecersiz.");
        }

        return Task.FromResult(new ProviderWebhookEvent(
            payload.EventId,
            payload.EventType,
            payload.Kind,
            payload.ProviderCheckoutSessionId,
            payload.ProviderPaymentId,
            payload.ProviderSubscriptionId,
            payload.Amount,
            payload.Currency,
            payload.OccurredAt,
            payload.FailureCode,
            payload.FailureMessage));
    }

    public Task<ProviderRefundResult> CreateRefundAsync(
        ProviderRefundRequest request,
        CancellationToken cancellationToken)
        => Task.FromResult(new ProviderRefundResult($"sandbox-refund-{request.RefundRequestId:N}"));

    public SignedSandboxWebhook CreateSuccessfulWebhook(
        CheckoutSession checkout,
        DateTimeOffset occurredAt)
    {
        var payload = new SandboxWebhookPayload(
            $"sandbox-event-{Guid.CreateVersion7():N}",
            "checkout.completed",
            ProviderPaymentEventKind.CheckoutCompleted,
            checkout.ProviderCheckoutSessionId,
            $"sandbox-payment-{checkout.Id:N}",
            $"sandbox-subscription-{checkout.Id:N}",
            checkout.Amount,
            checkout.Currency,
            occurredAt,
            null,
            null);

        var json = JsonSerializer.Serialize(payload, JsonOptions);
        return new SignedSandboxWebhook(json, Sign(json));
    }

    private string Sign(string payload)
    {
        var secret = Encoding.UTF8.GetBytes(options.Value.SandboxWebhookSecret);
        var signature = HMACSHA256.HashData(secret, Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(signature).ToLowerInvariant();
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private sealed record SandboxWebhookPayload(
        string EventId,
        string EventType,
        ProviderPaymentEventKind Kind,
        string? ProviderCheckoutSessionId,
        string? ProviderPaymentId,
        string? ProviderSubscriptionId,
        decimal? Amount,
        string? Currency,
        DateTimeOffset OccurredAt,
        string? FailureCode,
        string? FailureMessage);
}

public sealed record SignedSandboxWebhook(string Payload, string Signature);
