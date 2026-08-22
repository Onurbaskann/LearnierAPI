using Learnier.Application.Common.Abstractions;
using Learnier.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;

namespace Learnier.Infrastructure.Identity;

/// <summary>
/// Uyeligin izin kodlarini <c>membership_roles → role_permissions → permissions</c>
/// zinciri uzerinden cozer ve onbellekler.
/// </summary>
/// <remarks>
/// <para>
/// Izinler token'a gomulmedigi icin her yetkilendirme kontrolu bu zinciri sorgulamak
/// zorunda. Onbellek olmasaydi tek bir istek icinde bile ayni sorgu tekrarlanirdi.
/// </para>
/// <para>
/// Onbellek girdisi <see cref="MembershipTagPrefix"/> ile etiketlenir: bir uyeligin
/// rolleri degistiginde <c>RemoveByTagAsync</c> ile yalnizca o kullanicinin girdisi
/// dusurulebilir. Ayrica kisa bir sure sonu tanimli - etiketle dusurmeyi unutan bir
/// kod yolu olsa bile degisiklik en gec bu sure icinde yansir.
/// </para>
/// </remarks>
internal sealed class EfPermissionProvider(AppDbContext context, HybridCache cache) : IPermissionProvider
{
    /// <summary>
    /// Uyelik basina onbellek etiketinin oneki. Gecersiz kilmak icin:
    /// <c>cache.RemoveByTagAsync($"{MembershipTagPrefix}{membershipId}")</c>.
    /// </summary>
    public const string MembershipTagPrefix = "membership:";

    private static readonly HybridCacheEntryOptions CacheOptions = new()
    {
        Expiration = TimeSpan.FromMinutes(5),
        LocalCacheExpiration = TimeSpan.FromMinutes(5)
    };

    public async Task<IReadOnlySet<string>> GetPermissions(
        Guid membershipId,
        CancellationToken cancellationToken)
    {
        // HybridCache serilestirilebilir bir tip bekliyor; kume degil dizi onbelleklenir.
        var codes = await cache.GetOrCreateAsync(
            $"permissions:membership:{membershipId}",
            (Provider: this, MembershipId: membershipId),
            static (state, ct) => state.Provider.LoadPermissions(state.MembershipId, ct),
            CacheOptions,
            [$"{MembershipTagPrefix}{membershipId}"],
            cancellationToken);

        return new HashSet<string>(codes, StringComparer.Ordinal);
    }

    private async ValueTask<string[]> LoadPermissions(Guid membershipId, CancellationToken cancellationToken)
        => await context.MembershipRoles
            // Uyelik kimligi cagirana zaten dogrulanmis olarak geliyor; organizasyon
            // filtresi burada guvenlik saglamaz, yalnizca sorguyu gereksiz karmasiklastirirdi.
            .IgnoreQueryFilters([AppDbContext.TenantFilterName])
            .AsNoTracking()
            .Where(mr => mr.MembershipId == membershipId)
            .SelectMany(mr => mr.Role.Permissions)
            .Select(rp => rp.Permission.Code)
            .Distinct()
            .ToArrayAsync(cancellationToken);
}
