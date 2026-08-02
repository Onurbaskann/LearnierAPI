using Learnier.Application.Common.Abstractions;
using Microsoft.AspNetCore.Authorization;

namespace Learnier.WebApi.Authorization;

/// <summary>
/// Talep edilen iznin aktif uyelikte bulunup bulunmadigini kontrol eder.
/// </summary>
/// <remarks>
/// Izinler token'dan degil aktif organizasyondaki uyelikten cozulur. Bunun sebebi
/// ayni kullanicinin farkli organizasyonlarda farkli izinlere sahip olabilmesi:
/// bir kurumda egitmen olan kisi digerinde yalnizca ogrenci olabilir.
/// Organizasyon baglami yoksa izin de yoktur - istek reddedilir.
/// </remarks>
internal sealed class PermissionAuthorizationHandler(
    ICurrentTenant currentTenant,
    IPermissionProvider permissionProvider)
    : AuthorizationHandler<PermissionRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(requirement);

        if (currentTenant.MembershipId is not { } membershipId)
        {
            // Basarisiz isaretlenmez: baska bir handler ayni kosulu karsilayabilir.
            // Hicbiri karsilamazsa istek zaten reddedilir.
            return;
        }

        var permissions = await permissionProvider.GetPermissions(membershipId, CancellationToken.None);

        if (permissions.Contains(requirement.Permission))
        {
            context.Succeed(requirement);
        }
    }
}
