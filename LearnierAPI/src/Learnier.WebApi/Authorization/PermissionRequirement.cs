using Microsoft.AspNetCore.Authorization;

namespace Learnier.WebApi.Authorization;

/// <summary>
/// Belirli bir izin kodunu talep eden yetkilendirme kosulu.
/// </summary>
internal sealed class PermissionRequirement(string permission) : IAuthorizationRequirement
{
    public string Permission { get; } = permission;
}
