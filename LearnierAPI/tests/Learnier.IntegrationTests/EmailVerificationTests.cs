using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using Learnier.Application.Features.Authentication.Commands.LoginUser;
using Learnier.Application.Features.Authentication.Commands.RegisterUser;
using Learnier.Application.Features.Authentication.Commands.VerifyEmail;
using Learnier.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Learnier.IntegrationTests;

/// <summary>
/// E-posta dogrulama ucunun kendisi.
/// </summary>
/// <remarks>
/// Diger testler <c>ConfirmEmailAsync</c> kisayolunu kullanir; burada gercek uc
/// calistirilir. Token'in ham hali yalnizca gondericiye verildigi ve geri
/// okunamadigi icin test, ozetini kendisi hesaplayabildigi ikinci bir token yazar.
/// </remarks>
public sealed class EmailVerificationTests(AuthApiFixture fixture) : IClassFixture<AuthApiFixture>
{
    private const string Password = "CokGuvenli123";

    private static readonly Uri RegisterEndpoint = new("/api/v1/auth/register", UriKind.Relative);
    private static readonly Uri VerifyEndpoint = new("/api/v1/auth/verify-email", UriKind.Relative);
    private static readonly Uri LoginEndpoint = new("/api/v1/auth/login", UriKind.Relative);

    [Fact]
    public async Task NewAccount_IsPendingUntilVerified()
    {
        using var client = fixture.CreateClient();
        var email = UniqueEmail();

        (await Register(client, email)).StatusCode.ShouldBe(HttpStatusCode.OK);

        await using var context = fixture.CreateContext();

        var user = await context.Users.FirstAsync(
            u => u.Email == email, TestContext.Current.CancellationToken);

        user.Status.ShouldBe(UserStatus.Pending);
        user.EmailVerifiedAt.ShouldBeNull();

        // Kayit ayni zamanda dogrulama tokeni uretmis olmali.
        var tokenCount = await context.EmailVerificationTokens
            .CountAsync(t => t.UserId == user.Id, TestContext.Current.CancellationToken);

        tokenCount.ShouldBe(1);
    }

    [Fact]
    public async Task VerifyEndpoint_ActivatesAccountAndAllowsSignIn()
    {
        using var client = fixture.CreateClient();
        var email = UniqueEmail();

        await Register(client, email);

        var blocked = await SignIn(client, email);
        blocked.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var rawToken = await IssueKnownToken(email);

        var verified = await client.PostAsJsonAsync(
            VerifyEndpoint,
            new VerifyEmailCommand(rawToken),
            TestContext.Current.CancellationToken);

        verified.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var allowed = await SignIn(client, email);
        allowed.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    /// <summary>
    /// Token tek kullanimlik: ayni baglanti ikinci kez gecmez.
    /// </summary>
    [Fact]
    public async Task VerificationToken_CannotBeUsedTwice()
    {
        using var client = fixture.CreateClient();
        var email = UniqueEmail();

        await Register(client, email);

        var rawToken = await IssueKnownToken(email);

        var first = await client.PostAsJsonAsync(
            VerifyEndpoint,
            new VerifyEmailCommand(rawToken),
            TestContext.Current.CancellationToken);

        first.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var second = await client.PostAsJsonAsync(
            VerifyEndpoint,
            new VerifyEmailCommand(rawToken),
            TestContext.Current.CancellationToken);

        second.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UnknownToken_IsRejected()
    {
        using var client = fixture.CreateClient();

        var response = await client.PostAsJsonAsync(
            VerifyEndpoint,
            new VerifyEmailCommand($"bilinmeyen-{Guid.CreateVersion7():N}"),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    private static string UniqueEmail() => $"dogrulama-{Guid.CreateVersion7():N}@ornek.com";

    private static Task<HttpResponseMessage> Register(HttpClient client, string email)
        => client.PostAsJsonAsync(
            RegisterEndpoint,
            new RegisterUserCommand(email, Password, "Dogrulama", "Testi"),
            TestContext.Current.CancellationToken);

    private static Task<HttpResponseMessage> SignIn(HttpClient client, string email)
        => client.PostAsJsonAsync(
            LoginEndpoint,
            new LoginUserCommand(email, Password),
            TestContext.Current.CancellationToken);

    /// <summary>
    /// Ham degeri bilinen ikinci bir dogrulama tokeni yazar.
    /// </summary>
    private async Task<string> IssueKnownToken(string email)
    {
        var rawToken = $"test-{Guid.CreateVersion7():N}";

        await using var context = fixture.CreateContext();

        var user = await context.Users.FirstAsync(
            u => u.Email == email, TestContext.Current.CancellationToken);

        var now = DateTimeOffset.UtcNow;

        context.EmailVerificationTokens.Add(EmailVerificationToken.Issue(
            user.Id,
            Sha256Hex(rawToken),
            now,
            now.AddHours(24)));

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return rawToken;
    }

    /// <summary>Uygulamanin token ozetleme bicimiyle ayni: SHA-256, kucuk harfli onaltilik.</summary>
    private static string Sha256Hex(string value)
        => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
