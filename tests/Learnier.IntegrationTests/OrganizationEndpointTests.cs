using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Learnier.Application.Common.Security;
using Learnier.Application.Features.Authentication.Commands.LoginUser;
using Learnier.Application.Features.Organizations.Commands.CreateOrganization;
using Learnier.Application.Features.Organizations.Commands.InviteMember;
using Learnier.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Learnier.IntegrationTests;

/// <summary>
/// Organizasyon kurma, uye davet etme ve rol atama akisi.
/// </summary>
/// <remarks>
/// Izin altyapisinin ilk kez gercek bir uc uzerinde calistigi yer burasi: davet ve
/// rol atama <c>organization.member.manage</c> istiyor, kurucu ise sahip rolu
/// sayesinde bu izne sahip.
/// </remarks>
public sealed class OrganizationEndpointTests(AuthApiFixture fixture) : IClassFixture<AuthApiFixture>
{
    private static readonly Uri OrganizationsEndpoint = new("/api/organizations", UriKind.Relative);
    private static readonly Uri MembersEndpoint = new("/api/organizations/members", UriKind.Relative);
    private static readonly Uri LoginEndpoint = new("/api/auth/login", UriKind.Relative);

    private const string OrganizationHeader = "X-Organization-Id";

    [Fact]
    public async Task Founder_BecomesOwnerAndCanInviteMembers()
    {
        using var client = fixture.CreateClient();
        await SignIn(client, "ogrenci@hotmail.com", "ogrenci123");

        var created = await CreateOrganization(client);
        created.StatusCode.ShouldBe(HttpStatusCode.OK);

        var organization = await created.Content.ReadFromJsonAsync<CreateOrganizationResult>(
            TestContext.Current.CancellationToken);

        organization.ShouldNotBeNull();

        // Kurucu sahip rolunu almis olmali.
        await AssertHasRole(organization.MembershipId, SystemRoles.OrganizationOwner);

        client.DefaultRequestHeaders.Add(OrganizationHeader, organization.OrganizationId.ToString());

        var invited = await client.PostAsJsonAsync(
            MembersEndpoint,
            new InviteMemberCommand("ogretmen@hotmail.com", await SystemRoleId(SystemRoles.Instructor)),
            TestContext.Current.CancellationToken);

        invited.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task InviteMember_WithoutOrganizationContext_IsRejected()
    {
        using var client = fixture.CreateClient();
        await SignIn(client, "ogrenci@hotmail.com", "ogrenci123");

        // Organizasyon basligi yok: izin cozulemez, istek gecmemeli.
        var response = await client.PostAsJsonAsync(
            MembersEndpoint,
            new InviteMemberCommand("ogretmen@hotmail.com", await SystemRoleId(SystemRoles.Instructor)),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    /// <summary>
    /// Ogrenci rolu <c>organization.member.manage</c> tasimaz.
    /// </summary>
    [Fact]
    public async Task InviteMember_WithoutPermission_IsRejected()
    {
        using var client = fixture.CreateClient();
        await SignIn(client, "ogrenci@hotmail.com", "ogrenci123");

        var seededOrganizationId = await SeededOrganizationId();
        client.DefaultRequestHeaders.Add(OrganizationHeader, seededOrganizationId.ToString());

        var response = await client.PostAsJsonAsync(
            MembersEndpoint,
            new InviteMemberCommand("ogretmen@hotmail.com", await SystemRoleId(SystemRoles.Instructor)),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CreateOrganization_WithDuplicateSlug_IsRejected()
    {
        using var client = fixture.CreateClient();
        await SignIn(client, "ogrenci@hotmail.com", "ogrenci123");

        var slug = UniqueSlug();

        (await CreateOrganization(client, slug)).StatusCode.ShouldBe(HttpStatusCode.OK);

        var duplicate = await CreateOrganization(client, slug);

        duplicate.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        var problem = await duplicate.Content.ReadFromJsonAsync<JsonElement>(
            TestContext.Current.CancellationToken);

        problem.GetProperty("errorCode").GetString().ShouldBe("organization.slug_already_taken");

        // Parametreli mesaj: kisa ad metne yerlestirilmis olmali.
        var detail = problem.GetProperty("detail").GetString();

        detail.ShouldNotBeNull();
        detail.ShouldContain(slug);
    }

    [Fact]
    public async Task CreateOrganization_WithUnknownTimeZone_IsRejected()
    {
        using var client = fixture.CreateClient();
        await SignIn(client, "ogrenci@hotmail.com", "ogrenci123");

        var response = await client.PostAsJsonAsync(
            OrganizationsEndpoint,
            new
            {
                name = "Test Kurumu",
                slug = UniqueSlug(),
                // Enum metin olarak gonderiliyor; sayi beklenirse baglama basarisiz olur.
                organizationType = "Provider",
                timeZoneId = "Mars/Olympus",
                defaultCurrency = "TRY"
            },
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    private static string UniqueSlug() => $"kurum-{Guid.CreateVersion7():N}"[..24];

    private static Task<HttpResponseMessage> CreateOrganization(HttpClient client, string? slug = null)
        => client.PostAsJsonAsync(
            OrganizationsEndpoint,
            new
            {
                name = "Test Kurumu",
                slug = slug ?? UniqueSlug(),
                organizationType = "Provider",
                timeZoneId = "Europe/Istanbul",
                defaultCurrency = "TRY"
            },
            TestContext.Current.CancellationToken);

    private static async Task SignIn(HttpClient client, string email, string password)
    {
        var response = await client.PostAsJsonAsync(
            LoginEndpoint,
            new LoginUserCommand(email, password),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var session = await response.Content.ReadFromJsonAsync<LoginUserResult>(
            TestContext.Current.CancellationToken);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", session!.AccessToken);
    }

    private async Task<Guid> SystemRoleId(string code)
    {
        await using var context = fixture.CreateContext();

        var role = await context.Roles.FirstAsync(
            r => r.Code == code && r.OrganizationId == null,
            TestContext.Current.CancellationToken);

        return role.Id;
    }

    private async Task<Guid> SeededOrganizationId()
    {
        await using var context = fixture.CreateContext();

        var organization = await context.Organizations.FirstAsync(
            o => o.Slug == "learnier",
            TestContext.Current.CancellationToken);

        return organization.Id;
    }

    private async Task AssertHasRole(Guid membershipId, string roleCode)
    {
        await using var context = fixture.CreateContext();

        var codes = await context.MembershipRoles
            .Where(mr => mr.MembershipId == membershipId)
            .Select(mr => mr.Role.Code)
            .ToListAsync(TestContext.Current.CancellationToken);

        codes.ShouldContain(roleCode);
    }
}
