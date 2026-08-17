using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Learnier.Application.Common.Abstractions;
using Learnier.Application.Features.Authentication.Commands.LoginUser;
using Learnier.Application.Features.Billing.Commands.AddPlanPrice;
using Learnier.Application.Features.Billing.Commands.CreatePlan;
using Learnier.Application.Features.Organizations.Commands.CreateOrganization;
using Learnier.Application.Features.Subscriptions.Commands.CreateSubscription;
using Learnier.Domain.Billing;
using Learnier.Domain.Scheduling;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Learnier.IntegrationTests;

/// <summary>
/// Gercek satin alma ucu: yoneticinin satisa actigi plandan abonelik acilmasi.
/// </summary>
/// <remarks>
/// Demo satin almadan farki plan uretmemesidir; bu yuzden testler once plani
/// yonetim uclariyla kurar, sonra ayni istemciyle satin alir.
/// </remarks>
public sealed class SubscriptionPurchaseTests(AuthApiFixture fixture) : IClassFixture<AuthApiFixture>
{
    private const string OrganizationHeader = "X-Organization-Id";

    [Fact]
    public async Task Purchase_CreatesActiveSubscriptionWithPaymentAndCredits()
    {
        using var client = await NewOrganizationClient();
        var priceId = await PublishPlan(client, "Ingilizce Aylik", amount: 750m, credits: 8);

        var result = await Purchase(client, priceId);

        result.PaymentId.ShouldNotBeNull();
        result.CurrentPeriodEnd.ShouldBe(result.CurrentPeriodStart.AddMonths(1));

        var granted = result.GrantedCredits.ShouldHaveSingleItem();
        granted.Quantity.ShouldBe(8);
        granted.SessionType.ShouldBe(SessionType.Private);
        granted.LessonDurationMinutes.ShouldBe(50);

        await using var database = fixture.CreateContext();

        var subscription = await database.Subscriptions.SingleAsync(
            item => item.Id == result.SubscriptionId,
            TestContext.Current.CancellationToken);
        subscription.Status.ShouldBe(SubscriptionStatus.Active);
        subscription.PlanPriceId.ShouldBe(priceId);

        var payment = await database.Payments.SingleAsync(
            item => item.SubscriptionId == result.SubscriptionId,
            TestContext.Current.CancellationToken);
        payment.Amount.ShouldBe(750m);
        payment.Currency.ShouldBe("TRY");
        payment.Status.ShouldBe(PaymentStatus.Succeeded);

        // Kalan hak defterin toplamidir; ayri bir sayac tutulmaz.
        var ledger = await database.CreditLedger
            .Where(entry => entry.SubscriptionId == result.SubscriptionId)
            .ToListAsync(TestContext.Current.CancellationToken);

        var grant = ledger.ShouldHaveSingleItem();
        grant.TransactionType.ShouldBe(CreditTransactionType.PeriodGrant);
        grant.Quantity.ShouldBe(8);
        grant.PeriodStart.ShouldNotBeNull();
        grant.ExpiresAt.ShouldNotBeNull();
    }

    /// <summary>
    /// Aylik hak, abonelik doneminden once dolar: her ayin hakki o ay icinde
    /// kullanilmali, altisi birden birikmemeli.
    /// </summary>
    [Fact]
    public async Task Purchase_ExpiresFirstGrantAtEndOfCreditPeriod_NotSubscription()
    {
        using var client = await NewOrganizationClient();
        var priceId = await PublishPlan(
            client,
            "Alti Aylik",
            amount: 3000m,
            credits: 4,
            billingIntervalCount: 6);

        var result = await Purchase(client, priceId);

        result.CurrentPeriodEnd.ShouldBe(result.CurrentPeriodStart.AddMonths(6));
        result.GrantedCredits.ShouldHaveSingleItem()
            .ExpiresAt.ShouldBe(result.CurrentPeriodStart.AddMonths(1));
    }

    /// <summary>
    /// Katalog ekrani "zaten abonesin" durumunu plan kimligiyle gosterir; ad
    /// uzerinden eslestirme ayni adli iki planda yanilirdi.
    /// </summary>
    [Fact]
    public async Task ActivePackages_CarryPlanIdOfPurchasedPlan()
    {
        using var client = await NewOrganizationClient();
        var priceId = await PublishPlan(client, "Kimlikli Plan", amount: 500m, credits: 4);

        var result = await Purchase(client, priceId);

        var response = await client.GetAsync(
            new Uri("/api/v1/subscriptions/me/active-packages", UriKind.Relative),
            TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var packages = (await response.Content.ReadFromJsonAsync<IReadOnlyList<ActivePackageAccess>>(
            TestJson.Options,
            TestContext.Current.CancellationToken))!;

        packages
            .Where(item => item.SubscriptionId == result.SubscriptionId)
            .ShouldAllBe(item => item.PlanId == result.PlanId);
    }

    /// <summary>
    /// Yonetici panelinden acilan plan <c>MonthlyLessonCredits</c> alanini doldurmaz;
    /// yenileme o alana bakmis olsaydi satin alinan abonelik ilk aydan sonra hicbir
    /// hak almazdi.
    /// </summary>
    [Fact]
    public async Task Purchase_FromAdminPlan_RenewsNextPeriod()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        using var client = await NewOrganizationClient();
        var priceId = await PublishPlan(
            client,
            "Yenilenen",
            amount: 3000m,
            credits: 4,
            billingIntervalCount: 6);

        var result = await Purchase(client, priceId);

        var expiredAt = DateTimeOffset.UtcNow.AddMinutes(-1);

        await using (var database = fixture.CreateContext())
        {
            await database.CreditLedger
                .Where(entry => entry.SubscriptionId == result.SubscriptionId)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(entry => entry.PeriodStart, DateTimeOffset.UtcNow.AddMonths(-1))
                    .SetProperty(entry => entry.CreatedAt, DateTimeOffset.UtcNow.AddMonths(-1))
                    .SetProperty(entry => entry.ExpiresAt, expiredAt),
                    cancellationToken);
        }

        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider
                .GetRequiredService<ICreditPeriodRenewalProcessor>()
                .ProcessDueAsync(100, cancellationToken);
        }

        await using (var database = fixture.CreateContext())
        {
            var grants = await database.CreditLedger
                .Where(entry => entry.SubscriptionId == result.SubscriptionId
                                && entry.TransactionType == CreditTransactionType.PeriodGrant)
                .OrderBy(entry => entry.CreatedAt)
                .ToListAsync(cancellationToken);

            grants.Count.ShouldBe(2);
            grants[1].Quantity.ShouldBe(4);
            grants[1].PeriodStart!.Value.ShouldBe(expiredAt, tolerance: TimeSpan.FromMilliseconds(1));
        }
    }

    [Fact]
    public async Task Purchase_RejectsPlanThatIsNotOnSale()
    {
        using var client = await NewOrganizationClient();
        var priceId = await PublishPlan(client, "Taslak Plan", amount: 500m, credits: 4, activate: false);

        var response = await PurchaseRaw(client, priceId);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await ErrorCode(response)).ShouldBe("billing.plan_not_active");
    }

    [Fact]
    public async Task Purchase_RejectsArchivedPrice()
    {
        using var client = await NewOrganizationClient();
        var priceId = await PublishPlan(client, "Zamlanan", amount: 500m, credits: 4);

        // Yeni fiyat eskisini arsivler; arsivlenmis fiyattan satis yapilamaz.
        await AddPrice(client, await PlanIdOfPrice(priceId), 750m, billingIntervalCount: 1);

        var response = await PurchaseRaw(client, priceId);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await ErrorCode(response)).ShouldBe("billing.plan_price_not_active");
    }

    [Fact]
    public async Task Purchase_RejectsSecondSubscriptionToSamePlan()
    {
        using var client = await NewOrganizationClient();
        var priceId = await PublishPlan(client, "Tekrar Alinan", amount: 500m, credits: 4);

        await Purchase(client, priceId);
        var response = await PurchaseRaw(client, priceId);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await ErrorCode(response)).ShouldBe("billing.already_subscribed");
    }

    [Fact]
    public async Task Purchase_RejectsPriceOfAnotherOrganization()
    {
        using var owner = await NewOrganizationClient();
        var priceId = await PublishPlan(owner, "Baska Kurumun Plani", amount: 500m, credits: 4);

        using var stranger = await NewOrganizationClient();
        var response = await PurchaseRaw(stranger, priceId);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await ErrorCode(response)).ShouldBe("billing.plan_price_not_found");
    }

    /// <summary>
    /// Demo satin almanin urettigi plan kataloga girmez; kimligi elle yazilarak
    /// da satin alinamamalidir.
    /// </summary>
    [Fact]
    public async Task Purchase_RejectsSystemGeneratedPlan()
    {
        using var client = await NewOrganizationClient();
        var subjectId = await CreateSubject(client, "Ingilizce");

        var demo = await client.PostAsJsonAsync(
            new Uri("/api/v1/subscriptions/demo-purchases", UriKind.Relative),
            new
            {
                subjectId,
                lessonsPerWeek = 3,
                durationMonths = 6,
                lessonDurationMinutes = 50
            },
            TestContext.Current.CancellationToken);
        demo.StatusCode.ShouldBe(HttpStatusCode.OK);

        await using var database = fixture.CreateContext();
        var generatedPriceId = await database.PlanPrices
            .Where(price => price.Plan.IsSystemGenerated)
            .Select(price => price.Id)
            .SingleAsync(TestContext.Current.CancellationToken);

        var response = await PurchaseRaw(client, generatedPriceId);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await ErrorCode(response)).ShouldBe("billing.plan_not_purchasable");
    }

    [Fact]
    public async Task Purchase_RejectsUnknownPrice()
    {
        using var client = await NewOrganizationClient();

        var response = await PurchaseRaw(client, Guid.CreateVersion7());

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await ErrorCode(response)).ShouldBe("billing.plan_price_not_found");
    }

    private static async Task<CreateSubscriptionResult> Purchase(HttpClient client, Guid priceId)
    {
        var response = await PurchaseRaw(client, priceId);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        return (await response.Content.ReadFromJsonAsync<CreateSubscriptionResult>(
            TestJson.Options,
            TestContext.Current.CancellationToken))!;
    }

    private static Task<HttpResponseMessage> PurchaseRaw(HttpClient client, Guid priceId)
        => client.PostAsJsonAsync(
            new Uri("/api/v1/subscriptions", UriKind.Relative),
            new { planPriceId = priceId },
            TestContext.Current.CancellationToken);

    /// <summary>Plan kurar, fiyatlandirir, hak tanimini yazar ve satisa acar.</summary>
    private static async Task<Guid> PublishPlan(
        HttpClient client,
        string name,
        decimal amount,
        int credits,
        int billingIntervalCount = 1,
        bool activate = true)
    {
        var created = await client.PostAsJsonAsync(
            new Uri("/api/v1/plans", UriKind.Relative),
            new { name, catalogAccess = "All" },
            TestContext.Current.CancellationToken);
        created.StatusCode.ShouldBe(HttpStatusCode.OK);

        var planId = (await created.Content.ReadFromJsonAsync<CreatePlanResult>(
            TestJson.Options,
            TestContext.Current.CancellationToken))!.PlanId;

        var price = await AddPrice(client, planId, amount, billingIntervalCount);

        var entitlement = await client.PostAsJsonAsync(
            new Uri($"/api/v1/plans/{planId}/entitlements", UriKind.Relative),
            new
            {
                entitlementType = "LessonCredit",
                sessionType = "Private",
                resetPeriod = "Month",
                quantity = credits,
                lessonDurationMinutes = 50
            },
            TestContext.Current.CancellationToken);
        entitlement.StatusCode.ShouldBe(HttpStatusCode.OK);

        if (activate)
        {
            var activated = await client.PostAsync(
                new Uri($"/api/v1/plans/{planId}/activate", UriKind.Relative),
                content: null,
                TestContext.Current.CancellationToken);
            activated.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        }

        return price;
    }

    private static async Task<Guid> AddPrice(
        HttpClient client,
        Guid planId,
        decimal amount,
        int billingIntervalCount)
    {
        var response = await client.PostAsJsonAsync(
            new Uri($"/api/v1/plans/{planId}/prices", UriKind.Relative),
            new
            {
                currency = "TRY",
                amount,
                billingInterval = "Month",
                billingIntervalCount
            },
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        return (await response.Content.ReadFromJsonAsync<AddPlanPriceResult>(
            TestJson.Options,
            TestContext.Current.CancellationToken))!.PlanPriceId;
    }

    private async Task<Guid> PlanIdOfPrice(Guid priceId)
    {
        await using var database = fixture.CreateContext();

        return await database.PlanPrices
            .Where(price => price.Id == priceId)
            .Select(price => price.PlanId)
            .SingleAsync(TestContext.Current.CancellationToken);
    }

    private static async Task<Guid> CreateSubject(HttpClient client, string name)
    {
        var response = await client.PostAsJsonAsync(
            new Uri("/api/v1/subjects", UriKind.Relative),
            new { name, slug = $"buy-{Guid.CreateVersion7():N}"[..20] },
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var created = await response.Content.ReadFromJsonAsync<
            Application.Features.Catalog.Commands.CreateSubject.CreateSubjectResult>(
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
                    name = "Satin Alma Testi",
                    slug = $"buy-{Guid.CreateVersion7():N}"[..24],
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
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", session!.AccessToken);
    }

    private static async Task<string?> ErrorCode(HttpResponseMessage response)
    {
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(
            TestContext.Current.CancellationToken);

        return problem.GetProperty("errorCode").GetString();
    }
}
