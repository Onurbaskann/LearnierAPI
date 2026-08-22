using Learnier.Application.Common.Results;

namespace Learnier.Application.Features.Billing;

internal static class PaymentErrors
{
    public static Error CheckoutNotFound => Error.NotFound("payment.checkout_not_found");

    public static Error CheckoutNotReady => Error.Conflict("payment.checkout_not_ready");

    public static Error PaidPlanRequired => Error.Conflict("payment.paid_plan_required");

    public static Error AmountMismatch => Error.Conflict("payment.amount_mismatch");

    public static Error ProviderNotFound => Error.NotFound("payment.provider_not_found");

    public static Error WebhookInvalid => Error.Validation("payment.webhook_invalid");

    public static Error WebhookMissingCheckout => Error.Conflict("payment.webhook_missing_checkout");
}
