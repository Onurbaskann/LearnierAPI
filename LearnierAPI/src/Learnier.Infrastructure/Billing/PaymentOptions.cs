using System.ComponentModel.DataAnnotations;

namespace Learnier.Infrastructure.Billing;

public sealed class PaymentOptions
{
    public const string SectionName = "Payments";

    [Required]
    public string DefaultProvider { get; init; } = "sandbox";

    [Range(5, 1440)]
    public int CheckoutLifetimeMinutes { get; init; } = 30;

    [Required]
    [Url]
    public string PublicApiBaseUrl { get; init; } = "http://localhost:5031";

    [Required]
    [MinLength(32)]
    public string SandboxWebhookSecret { get; init; } = "local-sandbox-webhook-secret-change-me";
}
