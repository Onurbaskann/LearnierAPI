using Learnier.Application.Common.Abstractions;

namespace Learnier.Infrastructure.Identity.Placeholders;

/// <summary>
/// GECICI: hicbir izin dondurmez.
/// </summary>
/// <remarks>
/// Gercek uygulama <c>membership_roles</c> ve <c>role_permissions</c> uzerinden izinleri
/// cozup <c>HybridCache</c> ile onbellekleyecek; o tablolar Faz 1'de olusturulacak.
/// Bu tip Faz 1'de <c>PermissionProvider</c> ile degistirilmelidir.
/// Kapali varsayilan gerekcesi icin bkz. <see cref="DenyAllMembershipProvider"/>.
/// </remarks>
internal sealed class DenyAllPermissionProvider : IPermissionProvider
{
    private static readonly IReadOnlySet<string> None =
        new HashSet<string>(StringComparer.Ordinal);

    public Task<IReadOnlySet<string>> GetPermissions(
        Guid membershipId,
        CancellationToken cancellationToken)
        => Task.FromResult(None);
}
