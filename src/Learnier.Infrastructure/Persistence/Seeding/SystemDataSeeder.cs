using Learnier.Application.Common.Security;
using Learnier.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Learnier.Infrastructure.Persistence.Seeding;

/// <summary>
/// Izinleri ve sistem rollerini veritabanina yazar.
/// </summary>
/// <remarks>
/// <para>
/// Bu veri <b>referans verisidir</b>, ornek veri degil: her ortamda bulunmasi gerekir.
/// Yetkilendirme tamamen buna dayanir - izinler yoksa hicbir istek gecmez.
/// </para>
/// <para>
/// Tekrar calistirilabilir: mevcut kayitlar koda gore bulunur, yalnizca eksikler eklenir.
/// Bu sayede yeni bir izin veya rol tanimlandiginda seed yeniden calistirilarak
/// veritabani guncellenebilir; hicbir kayit silinmez veya uzerine yazilmaz.
/// </para>
/// </remarks>
internal sealed partial class SystemDataSeeder(AppDbContext context, ILogger<SystemDataSeeder> logger)
{
    // Loglar gelistirici icindir: Ingilizce yazilir ve lokalize edilmez.
    // Kaynak uretimli LoggerMessage kullaniliyor - bicimlendirme yalnizca
    // ilgili seviye aciksa calisir.
    [LoggerMessage(Level = LogLevel.Information, Message = "Seed: {Added} permission(s) added, {Total} total.")]
    private static partial void LogPermissionsSeeded(ILogger logger, int added, int total);

    [LoggerMessage(Level = LogLevel.Information, Message = "Seed: system role {RoleCode} created.")]
    private static partial void LogRoleCreated(ILogger logger, string roleCode);

    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        var permissionIds = await EnsurePermissionsAsync(cancellationToken);
        await EnsureSystemRolesAsync(permissionIds, cancellationToken);

        await context.SaveChangesAsync(cancellationToken);
    }

    private async Task<Dictionary<string, Guid>> EnsurePermissionsAsync(CancellationToken cancellationToken)
    {
        var existing = await context.Permissions
            .ToDictionaryAsync(p => p.Code, p => p.Id, StringComparer.Ordinal, cancellationToken);

        var added = 0;

        foreach (var code in PermissionCatalog.Codes.Where(c => !existing.ContainsKey(c)))
        {
            var permission = Permission.Create(code);
            context.Permissions.Add(permission);

            // Anahtar istemcide uretildigi icin kimlik kayittan once biliniyor;
            // rol eslemesi bu yuzden ayri bir SaveChanges beklemeden kurulabiliyor.
            existing[code] = permission.Id;
            added++;
        }

        LogPermissionsSeeded(logger, added, existing.Count);

        return existing;
    }

    private async Task EnsureSystemRolesAsync(
        Dictionary<string, Guid> permissionIds,
        CancellationToken cancellationToken)
    {
        // Sistem rolleri organizasyona ait degildir; Role tipi bilerek ITenantScoped
        // uygulamadigi icin burada organizasyon filtresi devrede degil.
        var existingRoles = await context.Roles
            .Include(r => r.Permissions)
            .Where(r => r.OrganizationId == null)
            .ToDictionaryAsync(r => r.Code, StringComparer.Ordinal, cancellationToken);

        foreach (var definition in SystemRoles.All)
        {
            if (!existingRoles.TryGetValue(definition.Code, out var role))
            {
                role = Role.CreateSystemRole(definition.Code, definition.Name);
                context.Roles.Add(role);
                LogRoleCreated(logger, definition.Code);
            }

            foreach (var permissionCode in definition.Permissions)
            {
                Grant(role, permissionIds[permissionCode]);
            }
        }
    }

    /// <summary>
    /// Role izin verir ve baglanti kaydini acikca ekler.
    /// </summary>
    /// <remarks>
    /// Acik <c>Add</c> sart: birincil anahtarlar istemcide uretildigi icin, kaydedilmis
    /// bir rolun koleksiyonuna sonradan eklenen baglantiyi EF "anahtari dolu, demek ki
    /// mevcut" sayip <c>Modified</c> isaretler ve hicbir satiri etkilemeyen bir UPDATE
    /// uretirdi. Rol yeni olusturulduysa bu cagri zararsizdir.
    /// </remarks>
    private void Grant(Role role, Guid permissionId)
    {
        if (role.Permissions.Any(p => p.PermissionId == permissionId))
        {
            return;
        }

        role.GrantPermission(permissionId);

        var link = role.Permissions.First(p => p.PermissionId == permissionId);
        context.Add(link);
    }
}
