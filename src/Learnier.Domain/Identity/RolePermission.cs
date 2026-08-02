using Learnier.Domain.Common;

namespace Learnier.Domain.Identity;

/// <summary>
/// Rol ile izin arasindaki baglanti.
/// </summary>
public sealed class RolePermission : Entity
{
    private RolePermission()
    {
    }

    public Guid RoleId { get; private set; }

    public Guid PermissionId { get; private set; }

    public Role Role { get; private set; } = null!;

    public Permission Permission { get; private set; } = null!;

    internal static RolePermission Create(Guid roleId, Guid permissionId)
        => new() { RoleId = roleId, PermissionId = permissionId };
}
