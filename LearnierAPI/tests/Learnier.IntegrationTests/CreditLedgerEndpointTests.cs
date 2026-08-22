using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Learnier.Application.Common.Abstractions;
using Learnier.Application.Features.Authentication.Commands.LoginUser;
using Learnier.Domain.Billing;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Learnier.IntegrationTests;

/// <summary>
/// Ders hakki defterinin hareket gecmisi ucu.
/// </summary>
/// <remarks>
/// <c>me/active-packages</c> kalan hakkin sonucunu verir; bu uc o sonucun nasil
/// olustugunu gosterir. Destekte "hakkim neden bu kadar" sorusunun tek yaniti
/// defterin kendisi oldugu icin sahiplik ve kiraci kontrolleri burada dogrulanir.
/// </remarks>
public sealed class CreditLedgerEndpointTests(AuthApiFixture fixture) : IClassFixture<AuthApiFixture>
{
    private const string OrganizationHeader = "X-Organization-Id";
    private const string LedgerPath = "/api/v1/subscriptions/credits/ledger";

    [Fact]
    public async Task Learner_SeesOwnLedger_WithRunningBalance()
    {
        using var client = await SignedInClient("ogrenci@hotmail.com", "ogrenci123");

        var entries = await client.GetFromJsonAsync<IReadOnlyList<CreditLedgerItem>>(
            new Uri(LedgerPath, UriKind.Relative),
            TestJson.Options,
            TestContext.Current.CancellationToken);

        var grant = entries.ShouldHaveSingleItem();
        grant.TransactionType.ShouldBe(CreditTransactionType.PeriodGrant);
        grant.Quantity.ShouldBe(12);

        // Tek hareket oldugu icin yuruyen bakiye o hareketin kendisine esit.
        grant.RunningBalance.ShouldBe(grant.Quantity);
        grant.BookingId.ShouldBeNull();
    }

    [Fact]
    public async Task Ledger_RequiresOrganizationContext()
    {
        using var client = fixture.CreateClient();
        await SignIn(client, "ogrenci@hotmail.com", "ogrenci123");

        // Bilerek X-Organization-Id gonderilmiyor: bu uc bir izin policy'si
        // tasimadigi icin kiraci baglamini handler'in kendisi zorlamak zorunda.
        var response = await client.GetAsync(
            new Uri(LedgerPath, UriKind.Relative),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await ErrorCode(response)).ShouldBe("tenant.organization_required");
    }

    [Fact]
    public async Task ActivePackages_RequireOrganizationContext()
    {
        using var client = fixture.CreateClient();
        await SignIn(client, "ogrenci@hotmail.com", "ogrenci123");

        // Kiraci yokken global query filter devre disi kalir; koruma olmasa
        // iki kuruma uye bir ogrenci ikisinin paketlerini birden gorurdu.
        var response = await client.GetAsync(
            new Uri("/api/v1/subscriptions/me/active-packages", UriKind.Relative),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await ErrorCode(response)).ShouldBe("tenant.organization_required");
    }

    [Fact]
    public async Task Learner_CannotSeeAnotherLearnersLedger()
    {
        using var client = await SignedInClient("ogrenci@hotmail.com", "ogrenci123");
        var otherUserId = await UserId("paketsiz@hotmail.com");

        var response = await client.GetAsync(
            new Uri($"{LedgerPath}?learnerUserId={otherUserId}", UriKind.Relative),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await ErrorCode(response)).ShouldBe("subscriptions.ledger_not_owned");
    }

    [Fact]
    public async Task Manager_CanSeeAnotherLearnersLedger()
    {
        using var client = await SignedInClient("admin@hotmail.com", "admin123");
        var learnerUserId = await UserId("ogrenci@hotmail.com");

        var entries = await client.GetFromJsonAsync<IReadOnlyList<CreditLedgerItem>>(
            new Uri($"{LedgerPath}?learnerUserId={learnerUserId}", UriKind.Relative),
            TestJson.Options,
            TestContext.Current.CancellationToken);

        entries.ShouldHaveSingleItem().Quantity.ShouldBe(12);
    }

    private async Task<HttpClient> SignedInClient(string email, string password)
    {
        var client = fixture.CreateClient();

        try
        {
            await SignIn(client, email, password);
            client.DefaultRequestHeaders.Add(
                OrganizationHeader,
                (await SeededOrganizationId()).ToString());
        }
        catch
        {
            client.Dispose();
            throw;
        }

        return client;
    }

    private static async Task SignIn(HttpClient client, string email, string password)
    {
        var response = await client.PostAsJsonAsync(
            new Uri("/api/v1/auth/login", UriKind.Relative),
            new LoginUserCommand(email, password),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var session = await response.Content.ReadFromJsonAsync<LoginUserResult>(
            TestJson.Options,
            TestContext.Current.CancellationToken);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", session!.AccessToken);
    }

    private async Task<Guid> SeededOrganizationId()
    {
        await using var database = fixture.CreateContext();

        return await database.Organizations
            .Select(organization => organization.Id)
            .FirstAsync(TestContext.Current.CancellationToken);
    }

    private async Task<Guid> UserId(string email)
    {
        await using var database = fixture.CreateContext();

        return await database.Users
            .Where(user => user.Email == email)
            .Select(user => user.Id)
            .SingleAsync(TestContext.Current.CancellationToken);
    }

    private static async Task<string?> ErrorCode(HttpResponseMessage response)
    {
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(
            TestContext.Current.CancellationToken);

        return problem.GetProperty("errorCode").GetString();
    }
}
