using System.Net;
using System.Net.Http.Json;
using Learnier.Application.Features.Authentication.Commands.LoginUser;
using Learnier.Application.Features.Authentication.Commands.RefreshAccessToken;
using Learnier.Application.Features.Authentication.Commands.RegisterUser;
using Learnier.Application.Features.Authentication.Commands.VerifyEmail;
using Learnier.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Learnier.IntegrationTests;

/// <summary>
/// Kayit → e-posta dogrulama → giris → token yenileme akisi.
/// </summary>
/// <remarks>
/// Dogrulama tokeninin ham hali yalnizca e-postaya gider ve veritabaninda ozeti
/// saklanir; test bu yuzden tokeni uretip ozetini eslestirmek yerine, kaydin
/// veritabanindaki halini kullanarak dogrulamayi tamamlar.
/// </remarks>
public sealed class RegistrationFlowTests(AuthApiFixture fixture) : IClassFixture<AuthApiFixture>
{
    private static readonly Uri RegisterEndpoint = new("/api/v1/auth/register", UriKind.Relative);
    private static readonly Uri VerifyEndpoint = new("/api/v1/auth/verify-email", UriKind.Relative);
    private static readonly Uri LoginEndpoint = new("/api/v1/auth/login", UriKind.Relative);
    private static readonly Uri RefreshEndpoint = new("/api/v1/auth/refresh", UriKind.Relative);

    private const string Password = "CokGuvenli123";

    [Fact]
    public async Task NewAccount_CannotSignInBeforeVerification()
    {
        using var client = fixture.CreateClient();
        var email = UniqueEmail();

        var registered = await Register(client, email);
        registered.StatusCode.ShouldBe(HttpStatusCode.OK);

        var result = await registered.Content.ReadFromJsonAsync<RegisterUserResult>(
            TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.VerificationRequired.ShouldBeTrue();

        var login = await client.PostAsJsonAsync(
            LoginEndpoint,
            new LoginUserCommand(email, Password),
            TestContext.Current.CancellationToken);

        login.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task VerifiedAccount_CanSignInAndRefresh()
    {
        using var client = fixture.CreateClient();
        var email = UniqueEmail();

        await Register(client, email);

        var verify = await client.PostAsJsonAsync(
            VerifyEndpoint,
            new VerifyEmailCommand(await IssueKnownVerificationToken(email)),
            TestContext.Current.CancellationToken);

        verify.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var login = await client.PostAsJsonAsync(
            LoginEndpoint,
            new LoginUserCommand(email, Password),
            TestContext.Current.CancellationToken);

        login.StatusCode.ShouldBe(HttpStatusCode.OK);

        var session = await login.Content.ReadFromJsonAsync<LoginUserResult>(
            TestContext.Current.CancellationToken);

        session.ShouldNotBeNull();
        session.RefreshToken.ShouldNotBeNullOrWhiteSpace();

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
    public async Task VerificationToken_CannotBeUsedTwice()
    {
        using var client = fixture.CreateClient();
        var email = UniqueEmail();

        await Register(client, email);

        var token = await IssueKnownVerificationToken(email);

        var first = await client.PostAsJsonAsync(
            VerifyEndpoint,
            new VerifyEmailCommand(token),
            TestContext.Current.CancellationToken);

        first.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var second = await client.PostAsJsonAsync(
            VerifyEndpoint,
            new VerifyEmailCommand(token),
            TestContext.Current.CancellationToken);

        second.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
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

    /// <summary>
    /// Kullanici icin ham degeri bilinen bir dogrulama tokeni yazar.
    /// </summary>
    /// <remarks>
    /// Kayit sirasinda uretilen tokenin ham hali yalnizca e-posta gondericisine
    /// verilir ve geri okunamaz. Test, ozeti kendisi hesaplayabildigi ikinci bir
    /// token ekleyerek dogrulama ucunu gercek veriyle calistirir.
    /// </remarks>
    private async Task<string> IssueKnownVerificationToken(string email)
    {
        var rawToken = $"test-{Guid.CreateVersion7():N}";

        await using var context = fixture.CreateContext();

        var user = await context.Users.FirstAsync(
            u => u.Email == email,
            TestContext.Current.CancellationToken);

        var now = DateTimeOffset.UtcNow;

        context.EmailVerificationTokens.Add(EmailVerificationToken.Issue(
            user.Id,
            Sha256Hex(rawToken),
            now,
            now.AddHours(24)));

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return rawToken;
    }

    /// <summary>
    /// Uygulamanin token ozetleme bicimiyle ayni: SHA-256, kucuk harfli onaltilik.
    /// </summary>
    private static string Sha256Hex(string value)
        => Convert.ToHexStringLower(
            System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(value)));
}
