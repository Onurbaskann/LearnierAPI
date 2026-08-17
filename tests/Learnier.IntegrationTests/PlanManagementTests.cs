using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Learnier.Application.Common.Abstractions;
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

    /// <summary>
    /// Kisitli kapsamli plan erisim satiri olmadan hicbir alani kapsamaz: ogrenci
    /// satin alir, kredisi olur ama hicbir derse rezervasyon yapamaz.
    /// </summary>
    [Fact]
    public async Task RestrictedPlan_WithoutAccess_CannotBeActivated()
    {
        using var client = await NewOrganizationClient();
        var planId = await CreatePlan(client, "Kapsamsiz", CatalogAccess.Restricted);
        await AddPrice(client, planId, 500m);
        await AddEntitlement(client, planId, quantity: 4);

        var response = await client.PostAsync(
            new Uri($"/api/v1/plans/{planId}/activate", UriKind.Relative),
            content: null,
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await ErrorCode(response)).ShouldBe("billing.plan_has_no_access");
    }

    [Fact]
    public async Task Plan_WithoutName_IsRejected()
    {
        using var client = await NewOrganizationClient();

        var response = await client.PostAsJsonAsync(
            new Uri("/api/v1/plans", UriKind.Relative),
            new { name = "  ", catalogAccess = "All" },
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ErrorCode(response)).ShouldBe("common.validation_failed");
    }

    [Fact]
    public async Task Plans_AreListedWithPriceHistoryAndEntitlements()
    {
        using var client = await NewOrganizationClient();

        var planId = await CreatePlan(client, "Listelenen", CatalogAccess.Restricted);
        var subjectId = await CreateSubject(client, "Ingilizce");

        (await client.PostAsJsonAsync(
            new Uri($"/api/v1/plans/{planId}/access", UriKind.Relative),
            new { subjectId },
            TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        await AddPrice(client, planId, 500m);
        await AddPrice(client, planId, 750m);
        await AddEntitlement(client, planId, quantity: 4, lessonDurationMinutes: 30);

        var plans = await ListPlans(client);
        var plan = plans.Single(item => item.Id == planId);

        plan.Status.ShouldBe(PlanStatus.Draft);
        plan.IsSystemGenerated.ShouldBeFalse();
        plan.ActivePrice.ShouldNotBeNull().Amount.ShouldBe(750m);

        // Fiyat guncellenmiyor, arsivleniyor: gecmis eksiksiz donmeli.
        plan.Prices.Count.ShouldBe(2);
        plan.Prices.Count(price => price.Status == PlanPriceStatus.Archived).ShouldBe(1);

        var entitlement = plan.Entitlements.ShouldHaveSingleItem();
        entitlement.Quantity.ShouldBe(4);
        entitlement.LessonDurationMinutes.ShouldBe(30);
        entitlement.ResetPeriod.ShouldBe(EntitlementResetPeriod.Month);

        plan.SubjectAccess.ShouldHaveSingleItem().Name.ShouldBe("Ingilizce");
        plan.CourseAccess.ShouldBeEmpty();
    }

    [Fact]
    public async Task Plans_OfOtherOrganization_AreNotVisible()
    {
        using var owner = await NewOrganizationClient();
        var planId = await CreatePlan(owner, "Gizli", CatalogAccess.All);

        using var stranger = await NewOrganizationClient();

        (await ListPlans(stranger)).ShouldNotContain(plan => plan.Id == planId);

        var detail = await stranger.GetAsync(
            new Uri($"/api/v1/plans/{planId}", UriKind.Relative),
            TestContext.Current.CancellationToken);

        detail.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await ErrorCode(detail)).ShouldBe("billing.plan_not_found");
    }

    [Fact]
    public async Task Catalog_ShowsOnlyActivatedPlans()
    {
        using var client = await NewOrganizationClient();

        var draftId = await CreatePlan(client, "Taslak", CatalogAccess.All);
        await AddPrice(client, draftId, 400m);
        await AddEntitlement(client, draftId, quantity: 4);

        var activeId = await CreatePlan(client, "Satista", CatalogAccess.All);
        await AddPrice(client, activeId, 600m);
        await AddEntitlement(client, activeId, quantity: 8);
        (await client.PostAsync(
            new Uri($"/api/v1/plans/{activeId}/activate", UriKind.Relative),
            content: null,
            TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var catalog = await ListCatalogPlans(client);

        catalog.ShouldNotContain(plan => plan.Id == draftId);

        var offered = catalog.ShouldHaveSingleItem();
        offered.Id.ShouldBe(activeId);
        offered.ActivePrice.Amount.ShouldBe(600m);
        offered.Entitlements.ShouldHaveSingleItem().Quantity.ShouldBe(8);
    }

    /// <summary>
    /// Demo satin alma her kosul kombinasyonu icin kendi planini dogurur ve
    /// aktiflestirir; bunlar yonetim listesinde gorunur ama kataloga girmez.
    /// </summary>
    [Fact]
    public async Task Catalog_HidesSystemGeneratedPlans()
    {
        using var client = await NewOrganizationClient();
        var subjectId = await CreateSubject(client, "Ingilizce");

        var purchase = await client.PostAsJsonAsync(
            new Uri("/api/v1/subscriptions/demo-purchases", UriKind.Relative),
            new
            {
                subjectId,
                lessonsPerWeek = 3,
                durationMonths = 6,
                lessonDurationMinutes = 50
            },
            TestContext.Current.CancellationToken);

        purchase.StatusCode.ShouldBe(HttpStatusCode.OK);

        var managed = await ListPlans(client);
        var generated = managed.ShouldHaveSingleItem();
        generated.IsSystemGenerated.ShouldBeTrue();
        generated.Status.ShouldBe(PlanStatus.Active);

        (await ListCatalogPlans(client)).ShouldBeEmpty();
    }

    /// <summary>
    /// Rezervasyon yetkilendirmesi uygun paketi ders suresiyle secer; suresi
    /// olmayan birebir kredi satin alinabilir ama hicbir oturumda kullanilamazdi.
    /// </summary>
    [Fact]
    public async Task PrivateCreditEntitlement_RequiresLessonDuration()
    {
        using var client = await NewOrganizationClient();
        var planId = await CreatePlan(client, "Suresiz", CatalogAccess.All);

        var response = await client.PostAsJsonAsync(
            new Uri($"/api/v1/plans/{planId}/entitlements", UriKind.Relative),
            new
            {
                entitlementType = "LessonCredit",
                sessionType = "Private",
                resetPeriod = "Month",
                quantity = 4
            },
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ErrorCode(response)).ShouldBe("billing.lesson_duration_required");
    }

    [Theory]
    [InlineData(45)]
    [InlineData(60)]
    public async Task PrivateCreditEntitlement_RejectsUnsupportedLessonDuration(int duration)
    {
        using var client = await NewOrganizationClient();
        var planId = await CreatePlan(client, $"Sure {duration}", CatalogAccess.All);

        var response = await client.PostAsJsonAsync(
            new Uri($"/api/v1/plans/{planId}/entitlements", UriKind.Relative),
            new
            {
                entitlementType = "LessonCredit",
                sessionType = "Private",
                resetPeriod = "Month",
                quantity = 4,
                lessonDurationMinutes = duration
            },
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ErrorCode(response)).ShouldBe("billing.lesson_duration_invalid");
    }

    /// <summary>Grup ve webinar oturumlari sure kirilimiyla satilmiyor.</summary>
    [Fact]
    public async Task GroupEntitlement_RejectsLessonDuration()
    {
        using var client = await NewOrganizationClient();
        var planId = await CreatePlan(client, "Grup", CatalogAccess.All);

        var response = await client.PostAsJsonAsync(
            new Uri($"/api/v1/plans/{planId}/entitlements", UriKind.Relative),
            new
            {
                entitlementType = "LessonCredit",
                sessionType = "Group",
                resetPeriod = "Month",
                quantity = 4,
                lessonDurationMinutes = 50
            },
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ErrorCode(response)).ShouldBe("billing.lesson_duration_not_allowed");
    }

    /// <summary>
    /// 30 ve 50 dakikalik krediler ayri haklardir: ikisi farkli oturum sureleriyle
    /// eslesir, bu yuzden ayni plan ikisini birden tasiyabilir.
    /// </summary>
    [Fact]
    public async Task Plan_CanCarryBothPrivateLessonDurations()
    {
        using var client = await NewOrganizationClient();
        var planId = await CreatePlan(client, "Karma", CatalogAccess.All);

        await AddEntitlement(client, planId, quantity: 4, lessonDurationMinutes: 30);
        await AddEntitlement(client, planId, quantity: 8, lessonDurationMinutes: 50);

        await using var database = fixture.CreateContext();

        var durations = await database.PlanEntitlements
            .Where(item => item.PlanId == planId)
            .Select(item => item.LessonDurationMinutes)
            .ToListAsync(TestContext.Current.CancellationToken);

        durations.OrderBy(value => value).ShouldBe([30, 50]);
    }

    [Fact]
    public async Task Entitlement_CannotBeDefinedTwiceForSameDuration()
    {
        using var client = await NewOrganizationClient();
        var planId = await CreatePlan(client, "Tekrar", CatalogAccess.All);
        await AddEntitlement(client, planId, quantity: 4, lessonDurationMinutes: 50);

        var response = await client.PostAsJsonAsync(
            new Uri($"/api/v1/plans/{planId}/entitlements", UriKind.Relative),
            new
            {
                entitlementType = "LessonCredit",
                sessionType = "Private",
                resetPeriod = "Month",
                quantity = 8,
                lessonDurationMinutes = 50
            },
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await ErrorCode(response)).ShouldBe("billing.entitlement_already_exists");
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

    private static async Task AddEntitlement(
        HttpClient client,
        Guid planId,
        int quantity,
        int lessonDurationMinutes = 50)
    {
        var response = await client.PostAsJsonAsync(
            new Uri($"/api/v1/plans/{planId}/entitlements", UriKind.Relative),
            new
            {
                entitlementType = "LessonCredit",
                sessionType = "Private",
                resetPeriod = "Month",
                quantity,
                lessonDurationMinutes
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

    private static async Task<IReadOnlyList<PlanDetail>> ListPlans(HttpClient client)
    {
        var response = await client.GetAsync(
            new Uri("/api/v1/plans", UriKind.Relative),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        return (await response.Content.ReadFromJsonAsync<IReadOnlyList<PlanDetail>>(
            TestJson.Options,
            TestContext.Current.CancellationToken))!;
    }

    private static async Task<IReadOnlyList<CatalogPlanItem>> ListCatalogPlans(HttpClient client)
    {
        var response = await client.GetAsync(
            new Uri("/api/v1/catalog/plans", UriKind.Relative),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        return (await response.Content.ReadFromJsonAsync<IReadOnlyList<CatalogPlanItem>>(
            TestJson.Options,
            TestContext.Current.CancellationToken))!;
    }

    private static async Task<string?> ErrorCode(HttpResponseMessage response)
    {
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(
            TestContext.Current.CancellationToken);

        return problem.GetProperty("errorCode").GetString();
    }
}
