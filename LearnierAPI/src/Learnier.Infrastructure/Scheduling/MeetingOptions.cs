using System.ComponentModel.DataAnnotations;

namespace Learnier.Infrastructure.Scheduling;

public sealed class MeetingOptions
{
    public const string SectionName = "Meetings";

    public bool ProvisioningEnabled { get; init; } = true;

    [Required]
    public string DefaultProvider { get; init; } = "sandbox";

    [Range(1, 1440)]
    public int ProvisioningIntervalMinutes { get; init; } = 1;

    [Range(1, 500)]
    public int BatchSize { get; init; } = 50;

    [Range(1, 20)]
    public int MaxAttempts { get; init; } = 5;

    [Required]
    [Url]
    public string PublicApiBaseUrl { get; init; } = "http://localhost:5031";
}
