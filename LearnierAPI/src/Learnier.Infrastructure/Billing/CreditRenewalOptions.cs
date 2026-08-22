using System.ComponentModel.DataAnnotations;

namespace Learnier.Infrastructure.Billing;

internal sealed class CreditRenewalOptions
{
    public const string SectionName = "CreditRenewal";

    public bool Enabled { get; init; } = true;

    [Range(1, 1440)]
    public int IntervalMinutes { get; init; } = 5;

    [Range(1, 1000)]
    public int BatchSize { get; init; } = 100;
}
