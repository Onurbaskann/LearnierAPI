using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Learnier.Application.Features.Accounts;
using Learnier.Application.Features.Accounts.Commands.UpdateMyContact;
using Learnier.Application.Features.Accounts.Commands.ChangeMyPassword;
using Learnier.Application.Features.Authentication.Commands.LoginUser;
using Learnier.Application.Features.Authentication.Commands.RegisterUser;
using Shouldly;

namespace Learnier.IntegrationTests;

public sealed class AccountEndpointTests(AuthApiFixture fixture) : IClassFixture<AuthApiFixture>
{
    [Fact]
    public async Task Password_CanBeChangedOnlyWithCurrentPassword()
    {
        using var client = fixture.CreateClient();
        var suffix = Guid.CreateVersion7().ToString("N");
        var email = $"parola-{suffix}@ornek.com";

        (await client.PostAsJsonAsync(
            new Uri("/api/v1/auth/register", UriKind.Relative),
            new RegisterUserCommand(email, "eskiParola123", "Parola", "Testi"),
            TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.OK);

        // Kayit sonrasi hesap dogrulanmamis durumda; giris icin once dogrulanir.
        await fixture.ConfirmEmailAsync(email, TestContext.Current.CancellationToken);

        var loginResponse = await client.PostAsJsonAsync(
            new Uri("/api/v1/auth/login", UriKind.Relative),
            new LoginUserCommand(email, "eskiParola123"),
            TestContext.Current.CancellationToken);
        var login = await loginResponse.Content.ReadFromJsonAsync<LoginUserResult>(
            TestContext.Current.CancellationToken);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login!.AccessToken);

        var wrongCurrent = await client.PutAsJsonAsync(
            new Uri("/api/v1/account/password", UriKind.Relative),
            new ChangeMyPasswordCommand("yanlisParola", "yeniParola123"),
            TestContext.Current.CancellationToken);
        wrongCurrent.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var changed = await client.PutAsJsonAsync(
            new Uri("/api/v1/account/password", UriKind.Relative),
            new ChangeMyPasswordCommand("eskiParola123", "yeniParola123"),
            TestContext.Current.CancellationToken);
        changed.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        using var loginClient = fixture.CreateClient();
        (await loginClient.PostAsJsonAsync(
            new Uri("/api/v1/auth/login", UriKind.Relative),
            new LoginUserCommand(email, "eskiParola123"),
            TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        (await loginClient.PostAsJsonAsync(
            new Uri("/api/v1/auth/login", UriKind.Relative),
            new LoginUserCommand(email, "yeniParola123"),
            TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Contact_CanBeReadAndUpdatedForCurrentUser()
    {
        using var client = fixture.CreateClient();
        var suffix = Guid.CreateVersion7().ToString("N");
        var email = $"hesap-{suffix}@ornek.com";

        var registration = await client.PostAsJsonAsync(
            new Uri("/api/v1/auth/register", UriKind.Relative),
            new RegisterUserCommand(email, "parola123", "Ilk", "Kullanici"),
            TestContext.Current.CancellationToken);

        registration.StatusCode.ShouldBe(HttpStatusCode.OK);

        await fixture.ConfirmEmailAsync(email, TestContext.Current.CancellationToken);

        var loginResponse = await client.PostAsJsonAsync(
            new Uri("/api/v1/auth/login", UriKind.Relative),
            new LoginUserCommand(email, "parola123"),
            TestContext.Current.CancellationToken);
        var login = await loginResponse.Content.ReadFromJsonAsync<LoginUserResult>(
            TestContext.Current.CancellationToken);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login!.AccessToken);
        client.DefaultRequestHeaders.Add(
            "X-Organization-Id", login.Memberships.ShouldHaveSingleItem().OrganizationId.ToString());

        var initial = await client.GetFromJsonAsync<AccountContact>(
            new Uri("/api/v1/account/contact", UriKind.Relative),
            TestContext.Current.CancellationToken);

        initial!.Email.ShouldBe(email);
        initial.FirstName.ShouldBe("Ilk");
        initial.Phone.ShouldBeNull();

        var newEmail = $"guncel-{suffix}@ornek.com";
        var update = await client.PutAsJsonAsync(
            new Uri("/api/v1/account/contact", UriKind.Relative),
            new UpdateMyContactCommand(newEmail, "Guncel", "Kullanici", "+90 555 111 22 33"),
            TestContext.Current.CancellationToken);

        update.StatusCode.ShouldBe(HttpStatusCode.OK);
        var updated = await update.Content.ReadFromJsonAsync<AccountContact>(
            TestContext.Current.CancellationToken);

        updated!.Email.ShouldBe(newEmail);
        updated.FirstName.ShouldBe("Guncel");
        updated.Phone.ShouldBe("+90 555 111 22 33");
    }
}
