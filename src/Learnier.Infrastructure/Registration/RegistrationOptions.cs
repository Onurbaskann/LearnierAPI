using System.ComponentModel.DataAnnotations;

namespace Learnier.Infrastructure.Registration;

internal sealed class RegistrationOptions
{
    public const string SectionName = "Registration";

    [Required(AllowEmptyStrings = false)]
    public string DefaultOrganizationSlug { get; init; } = "learnier";
}
