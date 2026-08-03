using Learnier.Application.Common.Abstractions;
using Learnier.Application.Common.Security;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Learnier.IntegrationTests;

/// <summary>
/// Izin cozumlemesinin <c>membership_roles → role_permissions → permissions</c>
/// zinciri uzerinden dogru calistigini dogrular.
/// </summary>
public sealed class PermissionProviderTests(PostgresFixture postgres)
    : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task ReturnsPermissionsGrantedThroughAssignedRoles()
    {
        var seed = await TestData.SeedAsync(
            postgres,
            rolePermissions: [[Permissions.Booking.Create, Permissions.Course.Read]],
            cancellationToken: TestContext.Current.CancellationToken);

        var permissions = await ResolveAsync(seed.MembershipId);

        permissions.ShouldContain(Permissions.Booking.Create);
        permissions.ShouldContain(Permissions.Course.Read);
        permissions.ShouldNotContain(Permissions.Course.Manage);
    }

    [Fact]
    public async Task ReturnsEmpty_WhenMembershipHasNoRoles()
    {
        var seed = await TestData.SeedAsync(
            postgres,
            cancellationToken: TestContext.Current.CancellationToken);

        var permissions = await ResolveAsync(seed.MembershipId);

        permissions.ShouldBeEmpty();
    }

    [Fact]
    public async Task DeduplicatesPermissionSharedByTwoRoles()
    {
        // Ayni izni veren iki rol atanirsa izin kumede bir kez bulunmali.
        var seed = await TestData.SeedAsync(
            postgres,
            rolePermissions: [[Permissions.Booking.Create], [Permissions.Booking.Create]],
            cancellationToken: TestContext.Current.CancellationToken);

        var permissions = await ResolveAsync(seed.MembershipId);

        permissions.Count.ShouldBe(1);
        permissions.ShouldContain(Permissions.Booking.Create);
    }

    [Fact]
    public async Task DoesNotLeakPermissionsOfAnotherMembership()
    {
        var granted = await TestData.SeedAsync(
            postgres,
            rolePermissions: [[Permissions.Subscription.Manage]],
            cancellationToken: TestContext.Current.CancellationToken);

        var other = await TestData.SeedAsync(
            postgres,
            cancellationToken: TestContext.Current.CancellationToken);

        var permissions = await ResolveAsync(other.MembershipId);

        permissions.ShouldBeEmpty();

        // Ilk uyeligin izni yerinde durmali - onbellek anahtari uyelik basina.
        var grantedPermissions = await ResolveAsync(granted.MembershipId);
        grantedPermissions.ShouldContain(Permissions.Subscription.Manage);
    }

    private async Task<IReadOnlySet<string>> ResolveAsync(Guid membershipId)
    {
        await using var scope = TestServices.CreateScope(postgres);
        var provider = scope.ServiceProvider.GetRequiredService<IPermissionProvider>();

        return await provider.GetPermissions(membershipId, TestContext.Current.CancellationToken);
    }
}
