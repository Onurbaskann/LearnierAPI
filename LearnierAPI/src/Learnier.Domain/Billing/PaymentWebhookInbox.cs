using Learnier.Domain.Common;

namespace Learnier.Domain.Billing;

/// <summary>
/// Saglayici webhook'unun idempotent ve yeniden denenebilir bicimde islenme kaydi.
/// </summary>
public sealed class PaymentWebhookInbox : Entity, IAuditableEntity
{
    private PaymentWebhookInbox()
    {
        Provider = string.Empty;
        ProviderEventId = string.Empty;
        EventType = string.Empty;
        Payload = string.Empty;
        PayloadSha256 = string.Empty;
    }

    public string Provider { get; private set; }

    public string ProviderEventId { get; private set; }

    public string EventType { get; private set; }

    public string Payload { get; private set; }

    public string PayloadSha256 { get; private set; }

    public WebhookProcessingStatus Status { get; private set; }

    public DateTimeOffset ReceivedAt { get; private set; }

    public DateTimeOffset? ProcessedAt { get; private set; }

    public int ProcessingAttemptCount { get; private set; }

    public string? LastError { get; private set; }

    public DateTimeOffset CreatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    public Guid? UpdatedBy { get; set; }

    public static PaymentWebhookInbox Receive(
        string provider,
        string providerEventId,
        string eventType,
        string payload,
        string payloadSha256,
        DateTimeOffset receivedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(providerEventId);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);
        ArgumentException.ThrowIfNullOrWhiteSpace(payload);
        ArgumentException.ThrowIfNullOrWhiteSpace(payloadSha256);

        if (payloadSha256.Trim().Length != 64)
        {
            throw new ArgumentException("Payload SHA-256 degeri 64 karakter olmalidir.", nameof(payloadSha256));
        }

        return new PaymentWebhookInbox
        {
            Provider = provider.Trim().ToLowerInvariant(),
            ProviderEventId = providerEventId.Trim(),
            EventType = eventType.Trim(),
            Payload = payload,
            PayloadSha256 = payloadSha256.Trim().ToLowerInvariant(),
            ReceivedAt = receivedAt,
            Status = WebhookProcessingStatus.Pending
        };
    }

    public void StartProcessing()
    {
        if (Status is WebhookProcessingStatus.Succeeded or WebhookProcessingStatus.Ignored)
        {
            throw new InvalidOperationException("Sonuclanmis webhook yeniden islenemez.");
        }

        ProcessingAttemptCount++;
        LastError = null;
        Status = WebhookProcessingStatus.Processing;
    }

    public void MarkSucceeded(DateTimeOffset processedAt)
    {
        EnsureProcessing();
        ProcessedAt = processedAt;
        Status = WebhookProcessingStatus.Succeeded;
    }

    public void MarkIgnored(DateTimeOffset processedAt)
    {
        EnsureProcessing();
        ProcessedAt = processedAt;
        Status = WebhookProcessingStatus.Ignored;
    }

    public void MarkFailed(string error)
    {
        EnsureProcessing();
        ArgumentException.ThrowIfNullOrWhiteSpace(error);
        LastError = error.Trim();
        Status = WebhookProcessingStatus.Failed;
    }

    private void EnsureProcessing()
    {
        if (Status is not WebhookProcessingStatus.Processing)
        {
            throw new InvalidOperationException("Webhook isleniyor durumunda olmalidir.");
        }
    }
}
