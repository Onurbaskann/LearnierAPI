using System.Net;
using System.Net.Http.Json;
using Learnier.Application.Features.Authentication.Commands.LoginUser;
using Learnier.Application.Features.Authentication.Commands.LogoutUser;
using Learnier.Application.Features.Authentication.Commands.RefreshAccessToken;
using Learnier.Application.Features.Authentication.Commands.RegisterUser;
using Shouldly;

namespace Learnier.IntegrationTests;

/// <summary>
/// Kayit → giris → token yenileme akisi.
/// </summary>
/// <remarks>
/// Acik kayitla olusan hesap hemen aktif olur ve varsayilan kuruma ogrenci olarak eklenir.
/// </remarks>
public sealed class RegistrationFlowTests(AuthApiFixture fixture) : IClassFixture<AuthApiFixture>
{
    private static readonly Uri RegisterEndpoint = new("/api/v1/auth/register", UriKind.Relative);
    private static readonly Uri LoginEndpoint = new("/api/v1/auth/login", UriKind.Relative);
    private static readonly Uri RefreshEndpoint = new("/api/v1/auth/refresh", UriKind.Relative);
    private static readonly Uri LogoutEndpoint = new("/api/v1/auth/logout", UriKind.Relative);

    private const string Password = "CokGuvenli123";

    [Fact]
    public async Task NewAccount_CanSignInImmediately()
    {
        using var client = fixture.CreateClient();
        var email = UniqueEmail();

        var registered = await Register(client, email);
        registered.StatusCode.ShouldBe(HttpStatusCode.OK);

        var result = await registered.Content.ReadFromJsonAsync<RegisterUserResult>(
            TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        var login = await client.PostAsJsonAsync(
            LoginEndpoint,
            new LoginUserCommand(email, Password),
            TestContext.Current.CancellationToken);

        login.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task NewAccount_CanRefreshSession()
    {
        using var client = fixture.CreateClient();
        var email = UniqueEmail();

        await Register(client, email);

        var login = await client.PostAsJsonAsync(
            LoginEndpoint,
            new LoginUserCommand(email, Password),
            TestContext.Current.CancellationToken);

        login.StatusCode.ShouldBe(HttpStatusCode.OK);

        var session = await login.Content.ReadFromJsonAsync<LoginUserResult>(
            TestContext.Current.CancellationToken);

        session.ShouldNotBeNull();
        session.RefreshToken.ShouldNotBeNullOrWhiteSpace();
        session.Memberships.Count.ShouldBe(1);
        session.Memberships[0].RoleCodes.ShouldContain("student");

        var refreshed = await client.PostAsJsonAsync(
            RefreshEndpoint,
            new RefreshAccessTokenCommand(session.RefreshToken),
            TestContext.Current.CancellationToken);

        refreshed.StatusCode.ShouldBe(HttpStatusCode.OK);

        var renewed = await refreshed.Content.ReadFromJsonAsync<RefreshAccessTokenResult>(
            TestContext.Current.CancellationToken);

        renewed.ShouldNotBeNull();

        // Rotasyon: yenileme tokeni her kullanimda degisir.
        renewed.RefreshToken.ShouldNotBe(session.RefreshToken);

        // Ve eskisi artik gecmemeli.
        var reused = await client.PostAsJsonAsync(
            RefreshEndpoint,
            new RefreshAccessTokenCommand(session.RefreshToken),
            TestContext.Current.CancellationToken);

        reused.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Logout_RevokesRefreshTokenAndRemainsIdempotent()
    {
        using var client = fixture.CreateClient();
        var email = UniqueEmail();

        await Register(client, email);
        var login = await client.PostAsJsonAsync(
            LoginEndpoint,
            new LoginUserCommand(email, Password),
            TestContext.Current.CancellationToken);
        var session = await login.Content.ReadFromJsonAsync<LoginUserResult>(
            TestContext.Current.CancellationToken);

        session.ShouldNotBeNull();

        var firstLogout = await client.PostAsJsonAsync(
            LogoutEndpoint,
            new LogoutUserCommand(session.RefreshToken),
            TestContext.Current.CancellationToken);
        firstLogout.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var refresh = await client.PostAsJsonAsync(
            RefreshEndpoint,
            new RefreshAccessTokenCommand(session.RefreshToken),
            TestContext.Current.CancellationToken);
        refresh.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        var secondLogout = await client.PostAsJsonAsync(
            LogoutEndpoint,
            new LogoutUserCommand(session.RefreshToken),
            TestContext.Current.CancellationToken);
        secondLogout.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Register_WithSameEmailInDifferentCase_IsRejected()
    {
        using var client = fixture.CreateClient();
        var email = UniqueEmail();

        (await Register(client, email)).StatusCode.ShouldBe(HttpStatusCode.OK);

        // citext: buyuk/kucuk harf farki yeni bir hesap acmaya yetmez.
        var duplicate = await Register(client, email.ToUpperInvariant());

        duplicate.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    private static string UniqueEmail() => $"kayit-{Guid.CreateVersion7():N}@ornek.com";

    private static Task<HttpResponseMessage> Register(HttpClient client, string email)
        => client.PostAsJsonAsync(
            RegisterEndpoint,
            new RegisterUserCommand(email, Password, "Test", "Kullanici"),
            TestContext.Current.CancellationToken);

}
