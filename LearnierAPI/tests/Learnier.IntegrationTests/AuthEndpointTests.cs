using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Learnier.Application.Features.Authentication.Commands.LoginUser;
using Microsoft.AspNetCore.Mvc;
using Shouldly;

namespace Learnier.IntegrationTests;

/// <summary>
/// Giris ucunun uctan uca dogrulanmasi.
/// </summary>
/// <remarks>
/// Zincirin tamami calisiyor: HTTP → validation filter → handler → veritabani →
/// token uretimi, ve donen token ile kimlik dogrulama + kiraci cozumlemesi.
/// </remarks>
public sealed class AuthEndpointTests(AuthApiFixture fixture) : IClassFixture<AuthApiFixture>
{
    private static readonly Uri LoginEndpoint = new("/api/v1/auth/login", UriKind.Relative);
    private static readonly Uri HealthEndpoint = new("/health", UriKind.Relative);

    private const string OrganizationHeader = "X-Organization-Id";

    [Fact]
    public async Task ValidCredentials_ReturnTokenAndMemberships()
    {
        using var client = fixture.CreateClient();

        var response = await client.PostAsJsonAsync(
            LoginEndpoint,
            new LoginUserCommand("ogrenci@hotmail.com", "ogrenci123"),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<LoginUserResult>(
            TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.AccessToken.ShouldNotBeNullOrWhiteSpace();
        result.ExpiresAt.ShouldBeGreaterThan(DateTimeOffset.UtcNow);
        result.User.Email.ShouldBe("ogrenci@hotmail.com");

        // Rol tek bir alan degil, uyelik basina tasinir.
        var membership = result.Memberships.ShouldHaveSingleItem();
        membership.OrganizationSlug.ShouldBe("learnier");
        membership.RoleCodes.ShouldContain("student");
    }

    [Fact]
    public async Task InstructorAccount_ReportsInstructorRole()
    {
        using var client = fixture.CreateClient();

        var result = await LoginAsync(client, "ogretmen@hotmail.com", "ogretmen123");

        result.Memberships.ShouldHaveSingleItem().RoleCodes.ShouldContain("instructor");
    }

    [Fact]
    public async Task WrongPassword_IsRejectedWithGenericCode()
    {
        using var client = fixture.CreateClient();

        var response = await client.PostAsJsonAsync(
            LoginEndpoint,
            new LoginUserCommand("ogrenci@hotmail.com", "yanlis-parola"),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await ReadErrorCodeAsync(response)).ShouldBe("auth.invalid_credentials");
    }

    [Fact]
    public async Task UnknownEmail_ReturnsSameCodeAsWrongPassword()
    {
        // Hesap sayimina karsi: iki durum disaridan ayirt edilememeli.
        using var client = fixture.CreateClient();

        var response = await client.PostAsJsonAsync(
            LoginEndpoint,
            new LoginUserCommand("boyle-biri-yok@ornek.com", "herhangi"),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await ReadErrorCodeAsync(response)).ShouldBe("auth.invalid_credentials");
    }

    [Fact]
    public async Task MissingPassword_IsRejectedByValidation()
    {
        using var client = fixture.CreateClient();

        var response = await client.PostAsJsonAsync(
            LoginEndpoint,
            new LoginUserCommand("ogrenci@hotmail.com", ""),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ReadErrorCodeAsync(response)).ShouldBe("common.validation_failed");
    }

    [Fact]
    public async Task MalformedEmail_IsRejectedByValidation()
    {
        using var client = fixture.CreateClient();

        var response = await client.PostAsJsonAsync(
            LoginEndpoint,
            new LoginUserCommand("e-posta-degil", "herhangi"),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ErrorMessage_IsLocalizedByRequestCulture()
    {
        using var client = fixture.CreateClient();
        client.DefaultRequestHeaders.AcceptLanguage.Add(new StringWithQualityHeaderValue("en"));

        var response = await client.PostAsJsonAsync(
            LoginEndpoint,
            new LoginUserCommand("ogrenci@hotmail.com", "yanlis-parola"),
            TestContext.Current.CancellationToken);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(
            TestContext.Current.CancellationToken);

        // Kod her dilde ayni, metin degisir.
        problem!.Detail.ShouldBe("Email or password is incorrect.");
    }

    [Fact]
    public async Task ErrorMessage_DefaultsToTurkish()
    {
        using var client = fixture.CreateClient();

        var response = await client.PostAsJsonAsync(
            LoginEndpoint,
            new LoginUserCommand("ogrenci@hotmail.com", "yanlis-parola"),
            TestContext.Current.CancellationToken);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(
            TestContext.Current.CancellationToken);

        problem!.Detail.ShouldBe("E-posta veya parola hatalı.");
    }

    [Fact]
    public async Task IssuedToken_PassesAuthenticationAndTenantResolution()
    {
        // Zincirin tamamini dogrular: token uretimi → JWT dogrulamasi →
        // CurrentUser → TenantResolutionMiddleware → uyelik sorgusu.
        using var client = fixture.CreateClient();

        var login = await LoginAsync(client, "ogrenci@hotmail.com", "ogrenci123");
        var organizationId = login.Memberships[0].OrganizationId;

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login.AccessToken);
        client.DefaultRequestHeaders.Add(OrganizationHeader, organizationId.ToString());

        var response = await client.GetAsync(HealthEndpoint, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task IssuedToken_IsRejectedForForeignOrganization()
    {
        using var client = fixture.CreateClient();

        var login = await LoginAsync(client, "ogrenci@hotmail.com", "ogrenci123");

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login.AccessToken);
        client.DefaultRequestHeaders.Add(OrganizationHeader, Guid.CreateVersion7().ToString());

        var response = await client.GetAsync(HealthEndpoint, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    private static async Task<LoginUserResult> LoginAsync(HttpClient client, string email, string password)
    {
        var response = await client.PostAsJsonAsync(
            LoginEndpoint,
            new LoginUserCommand(email, password),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        return (await response.Content.ReadFromJsonAsync<LoginUserResult>(
            TestContext.Current.CancellationToken))!;
    }

    /// <summary>
    /// Istemcinin metne degil koda gore dal ayirabilmesi icin yanitta tasinan kod.
    /// </summary>
    private static async Task<string?> ReadErrorCodeAsync(HttpResponseMessage response)
    {
        var payload = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        using var document = JsonDocument.Parse(payload);

        return document.RootElement.TryGetProperty("errorCode", out var code)
            ? code.GetString()
            : null;
    }
}
