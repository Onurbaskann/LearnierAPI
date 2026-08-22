using Learnier.Domain.Common;
using Learnier.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace Learnier.IntegrationTests;

/// <summary>
/// Testler icin kullanici, kurum, uyelik ve rol kaydi olusturur.
/// </summary>
/// <remarks>
/// <para>
/// Uyelik, rolleri ile birlikte <b>tek</b> <c>SaveChanges</c> icinde yazilir ve o ana
/// kadar hicbir ara kayit yapilmaz. Sebebi EF'in istemci tarafinda uretilen anahtarlarla
/// ilgili davranisi: <c>Entity.Id</c> yapicida doldugu icin, kaydedilmis bir uyeligin
/// koleksiyonuna sonradan eklenen <c>MembershipRole</c> "anahtari dolu, demek ki mevcut"
/// diye <c>Modified</c> isaretlenir ve INSERT yerine hicbir satiri etkilemeyen bir UPDATE
/// uretilir.
/// </para>
/// <para>
/// Ayni tuzak uygulama kodunda da gecerli: mevcut bir uyelige rol atayan bir handler
/// yeni baglantiyi <c>context.Add(...)</c> ile acikca eklemek zorunda.
/// </para>
/// </remarks>
internal static class TestData
{
    /// <param name="rolePermissions">
    /// Her eleman bir rol olur; icindeki kodlar o rolun izinleridir. Ayni izni iki
    /// role vererek tekillestirmenin dogrulanmasi da bu sayede mumkun.
    /// </param>
    public static async Task<SeedResult> SeedAsync(
        PostgresFixture postgres,
        MembershipStatus membershipStatus = MembershipStatus.Active,
        bool suspendUser = false,
        bool suspendOrganization = false,
        IReadOnlyList<string[]>? rolePermissions = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(postgres);

        var roles = rolePermissions ?? [];

        // Izin kayitlari once ve ayri bir context'te hazirlanir ki asil tohumlama
        // tek kayitta tamamlanabilsin.
        var permissionIds = await EnsurePermissionsAsync(
            postgres,
            roles.SelectMany(codes => codes).Distinct(StringComparer.Ordinal).ToArray(),
            cancellationToken);

        var now = DateTimeOffset.UtcNow;

        await using var context = postgres.CreateContext();

        // Her testin kendi kullanicisi ve kurumu olmali: e-posta ve slug benzersiz,
        // testler ise ayni veritabanini paylasiyor.
        var user = User.Register($"{Guid.CreateVersion7():N}@ornek.com", "Test", "Kullanici", "hash");
        user.ConfirmEmail(now);

        if (suspendUser)
        {
            user.Suspend();
        }

        Stamp(user, now);
        context.Users.Add(user);

        var organization = Organization.Create(
            "Test Kurumu",
            $"kurum-{Guid.CreateVersion7():N}",
            OrganizationType.Provider,
            "Europe/Istanbul",
            "TRY");

        if (suspendOrganization)
        {
            organization.Suspend();
        }

        var membership = organization.AddMember(user.Id, membershipStatus, now);

        Stamp(organization, now);
        Stamp(membership, now);
        context.Organizations.Add(organization);

        foreach (var codes in roles)
        {
            var role = Role.CreateSystemRole($"rol-{Guid.CreateVersion7():N}", "Test Rolu");
            Stamp(role, now);

            foreach (var code in codes)
            {
                role.GrantPermission(permissionIds[code]);
            }

            context.Roles.Add(role);
            membership.AssignRole(role.Id);
        }

        await context.SaveChangesAsync(cancellationToken);

        return new SeedResult(user.Id, organization.Id, membership.Id);
    }

    /// <summary>
    /// Verilen izin kodlarinin veritabaninda bulunmasini saglar ve kimliklerini dondurur.
    /// Izin kodlari tum sistemde tekildir; ayni kod ikinci kez eklenmez.
    /// </summary>
    private static async Task<Dictionary<string, Guid>> EnsurePermissionsAsync(
        PostgresFixture postgres,
        string[] codes,
        CancellationToken cancellationToken)
    {
        if (codes.Length is 0)
        {
            return [];
        }

        await using var context = postgres.CreateContext();

        var existing = await context.Permissions
            .Where(p => codes.Contains(p.Code))
            .ToDictionaryAsync(p => p.Code, p => p.Id, StringComparer.Ordinal, cancellationToken);

        foreach (var code in codes.Where(c => !existing.ContainsKey(c)))
        {
            var permission = Permission.Create(code);
            context.Permissions.Add(permission);
            existing[code] = permission.Id;
        }

        await context.SaveChangesAsync(cancellationToken);

        return existing;
    }

    /// <summary>
    /// Denetim alanlarini elle doldurur: fixture'in urettigi DbContext
    /// interceptor'lari icermez, dolayisiyla bunlari dolduran kod devrede degil.
    /// </summary>
    private static void Stamp(IAuditableEntity entity, DateTimeOffset now) => entity.CreatedAt = now;

    public sealed record SeedResult(Guid UserId, Guid OrganizationId, Guid MembershipId);
}
