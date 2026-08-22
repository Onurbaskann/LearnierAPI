using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Learnier.Application.Features.Authentication.Commands.LoginUser;
using Learnier.Application.Features.Billing.Commands.AddPlanEntitlement;
using Learnier.Application.Features.Billing.Commands.AddPlanPrice;
using Learnier.Application.Features.Billing.Commands.CreatePlan;
using Learnier.Application.Features.Catalog.Commands.CreateSubject;
using Learnier.Application.Features.Organizations.Commands.CreateOrganization;
using Learnier.Domain.Billing;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Learnier.IntegrationTests;

/// <summary>
/// Plan yonetiminin yonetici tarafi: olusturma, kapsam, fiyat surumleme,
/// hak tanimi ve satisa acma.
/// </summary>
/// <remarks>
/// Testler kendi organizasyonlarini kurar: tohumlanan hesaplarin hicbiri
/// <c>subscription.manage</c> tasimiyor, kurucu ise sahip rolu sayesinde tasiyor.
/// </remarks>
public sealed class PlanManagementTests(AuthApiFixture fixture) : IClassFixture<AuthApiFixture>
{
    private const string OrganizationHeader = "X-Organization-Id";

    [Fact]
    public async Task Plan_CanBeBuiltAndActivated()
    {
        using var client = await NewOrganizationClient();

        var planId = await CreatePlan(client, "Ingilizce Aylik", CatalogAccess.Restricted);
        var subjectId = await CreateSubject(client, "Ingilizce");

        var access = await client.PostAsJsonAsync(
            new Uri($"/api/v1/plans/{planId}/access", UriKind.Relative),
            new { subjectId },
            TestContext.Current.CancellationToken);
        access.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        await AddPrice(client, planId, 500m);
        await AddEntitlement(client, planId, quantity: 4);

        var activated = await client.PostAsync(
            new Uri($"/api/v1/plans/{planId}/activate", UriKind.Relative),
            content: null,
            TestContext.Current.CancellationToken);
        activated.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        await using var database = fixture.CreateContext();

        var plan = await database.SubscriptionPlans
            .SingleAsync(item => item.Id == planId, TestContext.Current.CancellationToken);
        plan.Status.ShouldBe(PlanStatus.Active);

        var granted = await database.PlanSubjectAccess
            .SingleAsync(item => item.PlanId == planId, TestContext.Current.CancellationToken);
        granted.SubjectId.ShouldBe(subjectId);
    }

    [Fact]
    public async Task NewPrice_ArchivesTheOldOne_InsteadOfUpdatingIt()
    {
        using var client = await NewOrganizationClient();
        var planId = await CreatePlan(client, "Fiyati Degisen", CatalogAccess.All);

        var first = await AddPrice(client, planId, 500m);
        first.ArchivedPriceId.ShouldBeNull();

        var second = await AddPrice(client, planId, 750m);

        // Eski kayit guncellenmemeli: hangi aboneligin hangi tutardan satildigi
        // izlenebilir kalmali (kaynak dokuman 8. bolum).
        second.ArchivedPriceId.ShouldBe(first.PlanPriceId);

        await using var database = fixture.CreateContext();

        var prices = await database.PlanPrices
            .Where(price => price.PlanId == planId)
            .OrderBy(price => price.Amount)
            .ToListAsync(TestContext.Current.CancellationToken);

        prices.Count.ShouldBe(2);
        prices[0].Amount.ShouldBe(500m);
        prices[0].Status.ShouldBe(PlanPriceStatus.Archived);
        prices[1].Amount.ShouldBe(750m);
        prices[1].Status.ShouldBe(PlanPriceStatus.Active);
    }

    [Fact]
    public async Task Plan_WithoutPrice_CannotBeActivated()
    {
        using var client = await NewOrganizationClient();
        var planId = await CreatePlan(client, "Fiyatsiz", CatalogAccess.All);
        await AddEntitlement(client, planId, quantity: 4);

        var response = await client.PostAsync(
            new Uri($"/api/v1/plans/{planId}/activate", UriKind.Relative),
            content: null,
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await ErrorCode(response)).ShouldBe("billing.plan_has_no_active_price");
    }

    [Fact]
    public async Task Plan_WithoutEntitlement_CannotBeActivated()
    {
        using var client = await NewOrganizationClient();
        var planId = await CreatePlan(client, "Haksiz", CatalogAccess.All);
        await AddPrice(client, planId, 500m);

        var response = await client.PostAsync(
            new Uri($"/api/v1/plans/{planId}/activate", UriKind.Relative),
            content: null,
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await ErrorCode(response)).ShouldBe("billing.plan_has_no_entitlement");
    }

    [Fact]
    public async Task UnlimitedEntitlement_RejectsQuantity()
    {
        using var client = await NewOrganizationClient();
        var planId = await CreatePlan(client, "Sinirsiz", CatalogAccess.All);

        // BookingAccess sinirsiz erisimi ifade eder; adet verilmesi karisikliga yol acar.
        var response = await client.PostAsJsonAsync(
            new Uri($"/api/v1/plans/{planId}/entitlements", UriKind.Relative),
            new
            {
                entitlementType = "BookingAccess",
                sessionType = "Group",
                resetPeriod = "Subscription",
                quantity = 4
            },
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ErrorCode(response)).ShouldBe("billing.quantity_not_allowed");
    }

    [Fact]
    public async Task MeteredEntitlement_RequiresQuantity()
    {
        using var client = await NewOrganizationClient();
        var planId = await CreatePlan(client, "Adetsiz", CatalogAccess.All);

        var response = await client.PostAsJsonAsync(
            new Uri($"/api/v1/plans/{planId}/entitlements", UriKind.Relative),
            new
            {
                entitlementType = "LessonCredit",
                sessionType = "Private",
                resetPeriod = "Month",
                quantity = (int?)null
            },
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ErrorCode(response)).ShouldBe("billing.quantity_required");
    }

    [Fact]
    public async Task Price_RejectsInvalidCurrency()
    {
        using var client = await NewOrganizationClient();
        var planId = await CreatePlan(client, "Para Birimsiz", CatalogAccess.All);

        var response = await client.PostAsJsonAsync(
            new Uri($"/api/v1/plans/{planId}/prices", UriKind.Relative),
            new
            {
                currency = "TRYY",
                amount = 500m,
                billingInterval = "Month",
                billingIntervalCount = 1
            },
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ErrorCode(response)).ShouldBe("billing.currency_invalid");
    }

    [Fact]
    public async Task PlanAccess_RequiresExactlyOneTarget()
    {
        using var client = await NewOrganizationClient();
        var planId = await CreatePlan(client, "Hedefsiz", CatalogAccess.Restricted);

        var response = await client.PostAsJsonAsync(
            new Uri($"/api/v1/plans/{planId}/access", UriKind.Relative),
            new { subjectId = (Guid?)null, courseId = (Guid?)null },
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ErrorCode(response)).ShouldBe("billing.access_target_invalid");
    }

    [Fact]
    public async Task Plan_IsIsolatedBetweenOrganizations()
    {
        using var owner = await NewOrganizationClient();
        var planId = await CreatePlan(owner, "Gizli Plan", CatalogAccess.All);

        using var stranger = await NewOrganizationClient();

        var response = await stranger.PostAsync(
            new Uri($"/api/v1/plans/{planId}/activate", UriKind.Relative),
            content: null,
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await ErrorCode(response)).ShouldBe("billing.plan_not_found");
    }

    [Fact]
    public async Task PlanManagement_RequiresSubscriptionManagePermission()
    {
        using var client = fixture.CreateClient();
        await SignIn(client, "ogrenci@hotmail.com", "ogrenci123");

        // Tohumlanan ogrenci hesabi kendi kurumunda subscription.manage tasimaz.
        client.DefaultRequestHeaders.Add(
            OrganizationHeader,
            (await SeededOrganizationId()).ToString());

        var response = await client.PostAsJsonAsync(
            new Uri("/api/v1/plans", UriKind.Relative),
            new { name = "Izinsiz Plan", catalogAccess = "All" },
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    private static async Task<Guid> CreatePlan(HttpClient client, string name, CatalogAccess catalogAccess)
    {
        var response = await client.PostAsJsonAsync(
            new Uri("/api/v1/plans", UriKind.Relative),
            new { name, catalogAccess = catalogAccess.ToString() },
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var created = await response.Content.ReadFromJsonAsync<CreatePlanResult>(
            TestJson.Options,
            TestContext.Current.CancellationToken);

        // Plan taslak baslar: fiyati ve haklari eksikken musteriye gorunmemeli.
        created!.Status.ShouldBe(PlanStatus.Draft);

        return created.PlanId;
    }

    private static async Task<AddPlanPriceResult> AddPrice(HttpClient client, Guid planId, decimal amount)
    {
        var response = await client.PostAsJsonAsync(
            new Uri($"/api/v1/plans/{planId}/prices", UriKind.Relative),
            new
            {
                currency = "TRY",
                amount,
                billingInterval = "Month",
                billingIntervalCount = 1
            },
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        return (await response.Content.ReadFromJsonAsync<AddPlanPriceResult>(
            TestJson.Options,
            TestContext.Current.CancellationToken))!;
    }

    private static async Task AddEntitlement(HttpClient client, Guid planId, int quantity)
    {
        var response = await client.PostAsJsonAsync(
            new Uri($"/api/v1/plans/{planId}/entitlements", UriKind.Relative),
            new
            {
                entitlementType = "LessonCredit",
                sessionType = "Private",
                resetPeriod = "Month",
                quantity
            },
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var created = await response.Content.ReadFromJsonAsync<AddPlanEntitlementResult>(
            TestJson.Options,
            TestContext.Current.CancellationToken);

        created!.EntitlementId.ShouldNotBe(Guid.Empty);
    }

    private static async Task<Guid> CreateSubject(HttpClient client, string name)
    {
        var response = await client.PostAsJsonAsync(
            new Uri("/api/v1/subjects", UriKind.Relative),
            new { name, slug = $"plan-{Guid.CreateVersion7():N}"[..20] },
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var created = await response.Content.ReadFromJsonAsync<CreateSubjectResult>(
            TestJson.Options,
            TestContext.Current.CancellationToken);

        return created!.SubjectId;
    }

    private async Task<HttpClient> NewOrganizationClient()
    {
        var client = fixture.CreateClient();

        try
        {
            await SignIn(client, "ogrenci@hotmail.com", "ogrenci123");

            var response = await client.PostAsJsonAsync(
                new Uri("/api/v1/organizations", UriKind.Relative),
                new
                {
                    name = "Plan Testi",
                    slug = $"plan-{Guid.CreateVersion7():N}"[..24],
                    organizationType = "Provider",
                    timeZoneId = "Europe/Istanbul",
                    defaultCurrency = "TRY"
                },
                TestContext.Current.CancellationToken);

            response.StatusCode.ShouldBe(HttpStatusCode.OK);

            var organization = await response.Content.ReadFromJsonAsync<CreateOrganizationResult>(
                TestContext.Current.CancellationToken);

            client.DefaultRequestHeaders.Add(
                OrganizationHeader,
                organization!.OrganizationId.ToString());
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
            .Where(organization => organization.Slug == "learnier")
            .Select(organization => organization.Id)
            .SingleAsync(TestContext.Current.CancellationToken);
    }

    private static async Task<string?> ErrorCode(HttpResponseMessage response)
    {
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(
            TestContext.Current.CancellationToken);

        return problem.GetProperty("errorCode").GetString();
    }
}
