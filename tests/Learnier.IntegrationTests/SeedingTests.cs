using Learnier.Application.Common.Abstractions;
using Learnier.Application.Common.Security;
using Learnier.Domain.Billing;
using Learnier.Domain.Identity;
using Learnier.Infrastructure.Persistence.Seeding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Learnier.IntegrationTests;

/// <summary>
/// Tohumlamanin dogru veriyi yazdigini ve tekrar calistirilabilir oldugunu dogrular.
/// </summary>
/// <remarks>
/// Tekrar calistirilabilirlik onemli: yeni bir izin veya rol eklendiginde seed
/// yeniden calistirilacak. Ikinci calisma kayit cogaltir veya hata verirse bu
/// yol kullanilamaz hale gelir.
/// </remarks>
public sealed class SeedingTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task SeedsAllPermissionsAndSystemRoles()
    {
        await using var provider = TestServices.BuildProvider(postgres);
        await DatabaseSeeder.RunAsync(provider, includeDevelopmentData: true, TestContext.Current.CancellationToken);

        await using var context = postgres.CreateContext();

        var permissions = await context.Permissions
            .Select(p => p.Code)
            .ToListAsync(TestContext.Current.CancellationToken);

        // Katalog koddan turedigi icin bu karsilastirma "kodda tanimli her izin
        // veritabaninda var mi" sorusunu dogrudan yanitlar.
        permissions.ShouldBe(PermissionCatalog.Codes, ignoreOrder: true);

        var roles = await context.Roles
            .Where(r => r.OrganizationId == null)
            .Select(r => r.Code)
            .ToListAsync(TestContext.Current.CancellationToken);

        roles.ShouldBe(SystemRoles.All.Select(r => r.Code), ignoreOrder: true);
    }

    [Fact]
    public async Task RunningTwice_DoesNotDuplicateData()
    {
        await using var provider = TestServices.BuildProvider(postgres);

        await DatabaseSeeder.RunAsync(provider, includeDevelopmentData: true, TestContext.Current.CancellationToken);
        var first = await CountAsync();

        await DatabaseSeeder.RunAsync(provider, includeDevelopmentData: true, TestContext.Current.CancellationToken);
        var second = await CountAsync();

        second.ShouldBe(first);
    }

    [Fact]
    public async Task DevelopmentAccounts_ResolveTheirPermissionsThroughRoles()
    {
        await using var provider = TestServices.BuildProvider(postgres);
        await DatabaseSeeder.RunAsync(provider, includeDevelopmentData: true, TestContext.Current.CancellationToken);

        await using var scope = provider.CreateAsyncScope();
        var memberships = scope.ServiceProvider.GetRequiredService<IMembershipProvider>();
        var permissions = scope.ServiceProvider.GetRequiredService<IPermissionProvider>();

        var (userId, organizationId) = await FindAccountAsync("ogrenci@hotmail.com");

        var membership = await memberships.FindActiveMembership(
            userId,
            organizationId,
            TestContext.Current.CancellationToken);

        membership.ShouldNotBeNull();

        var codes = await permissions.GetPermissions(
            membership.MembershipId,
            TestContext.Current.CancellationToken);

        // Ogrenci rolunun varsayilan izinleri.
        codes.ShouldContain(Permissions.Course.Read);
        codes.ShouldContain(Permissions.Booking.Create);

        // Ogrencinin sahip olmamasi gerekenler.
        codes.ShouldNotContain(Permissions.Course.Manage);
        codes.ShouldNotContain(Permissions.Booking.ManageAll);
    }

    [Fact]
    public async Task InstructorAccount_GetsInstructorPermissions()
    {
        await using var provider = TestServices.BuildProvider(postgres);
        await DatabaseSeeder.RunAsync(provider, includeDevelopmentData: true, TestContext.Current.CancellationToken);

        await using var scope = provider.CreateAsyncScope();
        var memberships = scope.ServiceProvider.GetRequiredService<IMembershipProvider>();
        var permissions = scope.ServiceProvider.GetRequiredService<IPermissionProvider>();

        var (userId, organizationId) = await FindAccountAsync("ogretmen@hotmail.com");

        var membership = await memberships.FindActiveMembership(
            userId,
            organizationId,
            TestContext.Current.CancellationToken);

        membership.ShouldNotBeNull();

        var codes = await permissions.GetPermissions(
            membership.MembershipId,
            TestContext.Current.CancellationToken);

        codes.ShouldContain(Permissions.Session.Create);
        codes.ShouldContain(Permissions.Student.ProgressRead);
        codes.ShouldNotContain(Permissions.Subscription.Manage);
    }

    [Fact]
    public async Task AdminAccount_GetsClubManagementPermission()
    {
        await using var provider = TestServices.BuildProvider(postgres);
        await DatabaseSeeder.RunAsync(provider, includeDevelopmentData: true, TestContext.Current.CancellationToken);

        await using var scope = provider.CreateAsyncScope();
        var memberships = scope.ServiceProvider.GetRequiredService<IMembershipProvider>();
        var permissions = scope.ServiceProvider.GetRequiredService<IPermissionProvider>();
        var (userId, organizationId) = await FindAccountAsync("admin@hotmail.com");

        var membership = await memberships.FindActiveMembership(
            userId,
            organizationId,
            TestContext.Current.CancellationToken);

        membership.ShouldNotBeNull();
        var codes = await permissions.GetPermissions(
            membership.MembershipId,
            TestContext.Current.CancellationToken);

        codes.ShouldContain(Permissions.Club.Manage);
        codes.ShouldContain(Permissions.Course.Manage);
        codes.ShouldContain(Permissions.Organization.MemberManage);
    }

    [Fact]
    public async Task DemoStudent_GetsRealEnglishPackageAccess()
    {
        await using var provider = TestServices.BuildProvider(postgres);
        await DatabaseSeeder.RunAsync(provider, includeDevelopmentData: true, TestContext.Current.CancellationToken);

        var (studentId, organizationId) = await FindAccountAsync("ogrenci@hotmail.com");
        await using var context = postgres.CreateContext();

        var hasEnglishAccess = await context.Subscriptions
            .Where(subscription =>
                subscription.OrganizationId == organizationId
                && subscription.SubscriberUserId == studentId
                && subscription.Status == SubscriptionStatus.Active)
            .SelectMany(subscription => context.PlanSubjectAccess
                .Where(access => access.PlanId == subscription.PlanPrice.PlanId)
                .Select(access => access.Subject.Name))
            .AnyAsync(name => name == "İngilizce", TestContext.Current.CancellationToken);

        hasEnglishAccess.ShouldBeTrue();

        var (packageFreeStudentId, _) = await FindAccountAsync("paketsiz@hotmail.com");
        (await context.Subscriptions.AnyAsync(
            subscription => subscription.SubscriberUserId == packageFreeStudentId,
            TestContext.Current.CancellationToken)).ShouldBeFalse();
    }

    [Fact]
    public async Task WithoutDevelopmentData_OnlyReferenceDataIsWritten()
    {
        // Bu test sinifin paylasilan veritabanini kullanamaz: kardes testler oraya
        // zaten ornek hesap yaziyor ve "yazilmamis olmali" iddiasi anlamsizlasirdi.
        // Bu yuzden kendi veritabanini kaldiriyor.
        var isolated = new PostgresFixture();
        await isolated.InitializeAsync();

        try
        {
            await using var provider = TestServices.BuildProvider(isolated);
            await DatabaseSeeder.RunAsync(
                provider,
                includeDevelopmentData: false,
                TestContext.Current.CancellationToken);

            await using var context = isolated.CreateContext();

            // Referans verisi yazilmis olmali...
            (await context.Permissions.CountAsync(TestContext.Current.CancellationToken))
                .ShouldBe(PermissionCatalog.Codes.Count);

            // ...ornek kurum ve hesaplar ise olusmamali.
            (await context.Users.CountAsync(TestContext.Current.CancellationToken)).ShouldBe(0);
            (await context.Organizations.CountAsync(TestContext.Current.CancellationToken)).ShouldBe(0);
        }
        finally
        {
            await isolated.DisposeAsync();
        }
    }

    private async Task<(Guid UserId, Guid OrganizationId)> FindAccountAsync(string email)
    {
        await using var context = postgres.CreateContext();

        var membership = await context.Memberships
            .IgnoreQueryFilters([Infrastructure.Persistence.AppDbContext.TenantFilterName])
            .Where(m => m.User.Email == email)
            .Select(m => new { m.UserId, m.OrganizationId })
            .SingleAsync(TestContext.Current.CancellationToken);

        return (membership.UserId, membership.OrganizationId);
    }

    private async Task<SeedCounts> CountAsync()
    {
        await using var context = postgres.CreateContext();

        return new SeedCounts(
            await context.Permissions.CountAsync(TestContext.Current.CancellationToken),
            await context.Roles.CountAsync(TestContext.Current.CancellationToken),
            await context.RolePermissions.CountAsync(TestContext.Current.CancellationToken),
            await context.Users.CountAsync(TestContext.Current.CancellationToken),
            await context.Organizations.CountAsync(TestContext.Current.CancellationToken),
            await context.Memberships
                .IgnoreQueryFilters([Infrastructure.Persistence.AppDbContext.TenantFilterName])
                .CountAsync(TestContext.Current.CancellationToken),
            await context.MembershipRoles
                .IgnoreQueryFilters([Infrastructure.Persistence.AppDbContext.TenantFilterName])
                .CountAsync(TestContext.Current.CancellationToken),
            await context.Subjects.CountAsync(TestContext.Current.CancellationToken),
            await context.SubscriptionPlans.CountAsync(TestContext.Current.CancellationToken),
            await context.PlanSubjectAccess.CountAsync(TestContext.Current.CancellationToken),
            await context.Subscriptions.CountAsync(TestContext.Current.CancellationToken));
    }

    private sealed record SeedCounts(
        int Permissions,
        int Roles,
        int RolePermissions,
        int Users,
        int Organizations,
        int Memberships,
        int MembershipRoles,
        int Subjects,
        int SubscriptionPlans,
        int PlanSubjectAccess,
        int Subscriptions);
}
