using System.ComponentModel.DataAnnotations;

namespace Learnier.Infrastructure.Identity;

internal sealed class PasswordResetOptions
{
    public const string SectionName = "PasswordReset";

    [Range(5, 1440)]
    public int TokenLifetimeMinutes { get; init; } = 30;
}
