using Learnier.Application.Common.Abstractions;
using Learnier.Domain.Identity;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Learnier.IntegrationTests;

/// <summary>
/// Uyelik cozumlemesinin gercek veritabanina karsi dogrulanmasi.
/// </summary>
/// <remarks>
/// Bu testlerin ikinci bir gorevi var: saglayici DI uzerinden cozuluyor, yani
/// kayit yanlis yapilirsa da testler kirilir.
/// </remarks>
public sealed class MembershipProviderTests(PostgresFixture postgres)
    : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task ActiveMembership_IsResolved()
    {
        var seed = await TestData.SeedAsync(
            postgres,
            cancellationToken: TestContext.Current.CancellationToken);

        var membership = await ResolveAsync(seed.UserId, seed.OrganizationId);

        membership.ShouldNotBeNull();
        membership.MembershipId.ShouldBe(seed.MembershipId);
        membership.OrganizationId.ShouldBe(seed.OrganizationId);
        membership.UserId.ShouldBe(seed.UserId);
    }

    [Fact]
    public async Task MembershipInAnotherOrganization_IsNotResolved()
    {
        // Tenant izolasyonunun ozu: istemci baska bir kurum kimligi gonderse bile
        // o kurumda uyeligi yoksa cozumleme basarisiz olmali.
        var seed = await TestData.SeedAsync(
            postgres,
            cancellationToken: TestContext.Current.CancellationToken);

        var membership = await ResolveAsync(seed.UserId, Guid.CreateVersion7());

        membership.ShouldBeNull();
    }

    [Fact]
    public async Task InvitedMembership_IsNotResolved()
    {
        // Davet edilmis ama kabul etmemis uyelik erisim vermez.
        var seed = await TestData.SeedAsync(
            postgres,
            membershipStatus: MembershipStatus.Invited,
            cancellationToken: TestContext.Current.CancellationToken);

        var membership = await ResolveAsync(seed.UserId, seed.OrganizationId);

        membership.ShouldBeNull();
    }

    [Fact]
    public async Task SuspendedUser_IsNotResolved()
    {
        var seed = await TestData.SeedAsync(
            postgres,
            suspendUser: true,
            cancellationToken: TestContext.Current.CancellationToken);

        var membership = await ResolveAsync(seed.UserId, seed.OrganizationId);

        membership.ShouldBeNull();
    }

    [Fact]
    public async Task SuspendedOrganization_IsNotResolved()
    {
        var seed = await TestData.SeedAsync(
            postgres,
            suspendOrganization: true,
            cancellationToken: TestContext.Current.CancellationToken);

        var membership = await ResolveAsync(seed.UserId, seed.OrganizationId);

        membership.ShouldBeNull();
    }

    private async Task<MembershipInfo?> ResolveAsync(Guid userId, Guid organizationId)
    {
        await using var scope = TestServices.CreateScope(postgres);
        var provider = scope.ServiceProvider.GetRequiredService<IMembershipProvider>();

        return await provider.FindActiveMembership(
            userId,
            organizationId,
            TestContext.Current.CancellationToken);
    }
}
