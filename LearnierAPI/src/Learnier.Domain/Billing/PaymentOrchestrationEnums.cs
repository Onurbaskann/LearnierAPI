namespace Learnier.Domain.Billing;

public enum CheckoutSessionStatus
{
    Created,
    Ready,
    Completed,
    Expired,
    Cancelled
}

public enum PaymentAttemptStatus
{
    Pending,
    RequiresAction,
    Succeeded,
    Failed,
    Cancelled
}

public enum WebhookProcessingStatus
{
    Pending,
    Processing,
    Succeeded,
    Failed,
    Ignored
}

public enum RefundRequestStatus
{
    Pending,
    Processing,
    Succeeded,
    Failed,
    Cancelled
}
