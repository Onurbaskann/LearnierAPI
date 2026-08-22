using Learnier.Domain.Common;

namespace Learnier.Domain.Identity;

/// <summary>
/// Bir izin kumesine verilen ad.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="OrganizationId"/> bos ise rol sistem genelindedir (ornegin
/// <c>instructor</c>, <c>student</c>); dolu ise yalnizca o organizasyona ozeldir.
/// </para>
/// <para>
/// Bu tip bilerek <c>ITenantScoped</c> uygulamaz: organizasyon kimligi bos olabildigi
/// icin otomatik filtre sistem rollerini de gizlerdi. Filtreleme sorgu tarafinda
/// acikca yapilir - bkz. <see cref="IsVisibleTo"/>.
/// </para>
/// </remarks>
public sealed class Role : Entity, IAuditableEntity
{
    private readonly List<RolePermission> _permissions = [];

    private Role()
    {
        Code = string.Empty;
        Name = string.Empty;
    }

    /// <summary>Sistem rolleri icin <see langword="null"/>.</summary>
    public Guid? OrganizationId { get; private set; }

    /// <summary>Makine tarafindan kullanilan sabit kod, ornegin <c>org_admin</c>.</summary>
    public string Code { get; private set; }

    /// <summary>
    /// Gorunen ad. Sistem rollerinde bu deger yalnizca yedektir; arayuz
    /// kaynak dosyasindaki cevirisini gostermelidir.
    /// </summary>
    public string Name { get; private set; }

    public bool IsSystem { get; private set; }

    public IReadOnlyCollection<RolePermission> Permissions => _permissions.AsReadOnly();

    public DateTimeOffset CreatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    public Guid? UpdatedBy { get; set; }

    public static Role CreateSystemRole(string code, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return new Role
        {
            OrganizationId = null,
            Code = code,
            Name = name,
            IsSystem = true
        };
    }

    public static Role CreateOrganizationRole(Guid organizationId, string code, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return new Role
        {
            OrganizationId = organizationId,
            Code = code,
            Name = name,
            IsSystem = false
        };
    }

    /// <summary>
    /// Rolun verilen organizasyonda kullanilabilir olup olmadigi:
    /// sistem rolleri her yerde, ozel roller yalnizca sahibi olan kurumda.
    /// </summary>
    public bool IsVisibleTo(Guid organizationId)
        => OrganizationId is null || OrganizationId == organizationId;

    public void GrantPermission(Guid permissionId)
    {
        if (_permissions.Exists(p => p.PermissionId == permissionId))
        {
            return;
        }

        _permissions.Add(RolePermission.Create(Id, permissionId));
    }

    public void RevokePermission(Guid permissionId)
        => _permissions.RemoveAll(p => p.PermissionId == permissionId);
}
