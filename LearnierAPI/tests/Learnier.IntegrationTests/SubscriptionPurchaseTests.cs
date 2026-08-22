using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Learnier.Application.Common.Abstractions;
using Learnier.Application.Features.Authentication.Commands.LoginUser;
using Learnier.Application.Features.Billing.Commands.AddPlanPrice;
using Learnier.Application.Features.Billing.Commands.CreatePlan;
using Learnier.Application.Features.Billing.Commands.CreateCheckout;
using Learnier.Application.Features.Billing.Commands.ProcessPaymentWebhook;
using Learnier.Application.Features.Catalog.Commands.CreateCourse;
using Learnier.Application.Features.Organizations.Commands.CreateOrganization;
using Learnier.Application.Features.Scheduling.Commands.OpenInstructorSlot;
using Learnier.Application.Features.Subscriptions.Commands.CreateSubscription;
using Learnier.Application.Features.Teaching.Commands.CreateInstructorProfile;
using Learnier.Domain.Billing;
using Learnier.Domain.Identity;
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
    public async Task SandboxCheckout_ActivatesSubscriptionAndCreditsOnlyAfterWebhook()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = await NewOrganizationClient();
        var priceId = await PublishPlan(client, "Webhook Paketi", amount: 825m, credits: 6);

        var checkoutResponse = await client.PostAsJsonAsync(
            new Uri("/api/v1/payments/checkouts", UriKind.Relative),
            new { planPriceId = priceId, idempotencyKey = $"test-{Guid.CreateVersion7():N}" },
            cancellationToken);
        checkoutResponse.StatusCode.ShouldBe(
            HttpStatusCode.OK,
            await checkoutResponse.Content.ReadAsStringAsync(cancellationToken));

        var checkout = (await checkoutResponse.Content.ReadFromJsonAsync<CreateCheckoutResult>(
            TestJson.Options,
            cancellationToken))!;

        await using (var database = fixture.CreateContext())
        {
            (await database.Subscriptions.CountAsync(
                s => s.PlanPriceId == priceId,
                cancellationToken)).ShouldBe(0);
            var persistedCheckout = await database.CheckoutSessions.SingleAsync(
                c => c.Id == checkout.CheckoutSessionId,
                cancellationToken);
            persistedCheckout.Status.ShouldBe(CheckoutSessionStatus.Ready);
        }

        var completion = await client.PostAsync(
            new Uri(
                $"/api/v1/payments/sandbox/checkouts/{checkout.CheckoutSessionId}/complete",
                UriKind.Relative),
            content: null,
            cancellationToken);
        completion.StatusCode.ShouldBe(
            HttpStatusCode.OK,
            await completion.Content.ReadAsStringAsync(cancellationToken));

        var webhook = (await completion.Content.ReadFromJsonAsync<ProcessPaymentWebhookResult>(
            TestJson.Options,
            cancellationToken))!;
        webhook.Status.ShouldBe(WebhookProcessingStatus.Succeeded);

        // Ayni checkout'un yeniden tamamlanmasi finansal kayitlari cogaltmamalidir.
        (await client.PostAsync(
            new Uri(
                $"/api/v1/payments/sandbox/checkouts/{checkout.CheckoutSessionId}/complete",
                UriKind.Relative),
            content: null,
            cancellationToken)).StatusCode.ShouldBe(HttpStatusCode.OK);

        await using (var database = fixture.CreateContext())
        {
            var subscription = await database.Subscriptions.SingleAsync(
                s => s.PlanPriceId == priceId,
                cancellationToken);
            subscription.Status.ShouldBe(SubscriptionStatus.Active);
            subscription.PaymentProvider.ShouldBe("sandbox");

            var payment = await database.Payments.SingleAsync(
                p => p.SubscriptionId == subscription.Id,
                cancellationToken);
            payment.Status.ShouldBe(PaymentStatus.Succeeded);
            payment.Amount.ShouldBe(825m);

            var credit = await database.CreditLedger.SingleAsync(
                entry => entry.SubscriptionId == subscription.Id,
                cancellationToken);
            credit.Quantity.ShouldBe(6);

            (await database.PaymentAttempts.CountAsync(
                attempt => attempt.CheckoutSessionId == checkout.CheckoutSessionId,
                cancellationToken)).ShouldBe(1);
        }
    }

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

    /// <summary>
    /// Faz 0.0'in uctan uca kaniti: yonetici planiyla rezervasyon.
    /// </summary>
    /// <remarks>
    /// Rezervasyon yetkilendirmesi plan uzerindeki denormalize alanlardan degil
    /// hak tanimindan turetiliyor. Yonetici planinda o alanlar bos ve kapsam
    /// <c>CatalogAccess.All</c>; eski kod bu iki nedenle de rezervasyonu
    /// reddederdi. Gercek satin alma ucu olmadan bu senaryo ancak elle DB satiri
    /// seedleyerek kurulabildigi icin Faz 0.4'e birakilmisti.
    /// </remarks>
    [Fact]
    public async Task Booking_WorksWithPlanPurchasedFromCatalog()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        using var client = await NewOrganizationClient();
        var organizationId = Guid.Parse(
            client.DefaultRequestHeaders.GetValues(OrganizationHeader).Single());

        var subjectId = await CreateSubject(client, "Ingilizce");
        var courseId = await CreatePublishedPrivateCourse(client, subjectId);

        // Plan hicbir alana acik degil, kapsami tum katalog: erisim satiri
        // olmadan da rezervasyona izin vermeli.
        var priceId = await PublishPlan(client, "Katalog Plani", amount: 900m, credits: 4);
        var subscription = await Purchase(client, priceId);

        var sessionId = await OpenInstructorSlot(client, organizationId, courseId, subjectId);

        var booking = await client.PostAsJsonAsync(
            new Uri($"/api/v1/sessions/{sessionId}/bookings", UriKind.Relative),
            new { learnerUserId = (Guid?)null, lessonDurationMinutes = 50 },
            cancellationToken);

        booking.StatusCode.ShouldBe(
            HttpStatusCode.OK,
            await booking.Content.ReadAsStringAsync(cancellationToken));

        await using var database = fixture.CreateContext();

        var entries = await database.CreditLedger
            .Where(entry => entry.SubscriptionId == subscription.SubscriptionId)
            .ToListAsync(cancellationToken);

        entries.Count(entry => entry.TransactionType == CreditTransactionType.PeriodGrant)
            .ShouldBe(1);

        var reserve = entries.Single(
            entry => entry.TransactionType == CreditTransactionType.Reserve);
        reserve.Quantity.ShouldBe(-1);

        // Rezervasyon hakki dusurur; kalan bakiye defterin toplamidir.
        entries.Sum(entry => entry.Quantity).ShouldBe(3);
    }

    /// <summary>Egitmeni kurar, bir slot acar ve olusan oturumu dondurur.</summary>
    private async Task<Guid> OpenInstructorSlot(
        HttpClient client,
        Guid organizationId,
        Guid courseId,
        Guid subjectId)
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        Guid instructorRoleId;
        await using (var database = fixture.CreateContext())
        {
            instructorRoleId = await database.Roles
                .Where(role => role.Code == "instructor" && role.OrganizationId == null)
                .Select(role => role.Id)
                .FirstAsync(cancellationToken);
        }

        (await client.PostAsJsonAsync(
            new Uri("/api/v1/organizations/members", UriKind.Relative),
            new { email = "ogretmen@hotmail.com", roleId = instructorRoleId },
            cancellationToken)).StatusCode.ShouldBe(HttpStatusCode.OK);

        Guid membershipId;
        await using (var database = fixture.CreateContext())
        {
            var membership = await database.Memberships
                .Where(item => item.OrganizationId == organizationId
                               && item.Status == MembershipStatus.Invited)
                .FirstAsync(cancellationToken);

            membership.Accept(DateTimeOffset.UtcNow);
            await database.SaveChangesAsync(cancellationToken);
            membershipId = membership.Id;
        }

        var profile = await client.PostAsJsonAsync(
            new Uri("/api/v1/instructors", UriKind.Relative),
            new { membershipId, timeZoneId = "Europe/Istanbul" },
            cancellationToken);
        profile.StatusCode.ShouldBe(HttpStatusCode.OK);

        var profileId = (await profile.Content.ReadFromJsonAsync<CreateInstructorProfileResult>(
            TestJson.Options,
            cancellationToken))!.ProfileId;

        (await client.PostAsync(
            new Uri($"/api/v1/instructors/{profileId}/activate", UriKind.Relative),
            content: null,
            cancellationToken)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await client.PostAsJsonAsync(
            new Uri($"/api/v1/instructors/{profileId}/subjects", UriKind.Relative),
            new { subjectId, levelId = (Guid?)null },
            cancellationToken)).StatusCode.ShouldBe(HttpStatusCode.OK);

        using var instructorClient = fixture.CreateClient();
        await SignIn(instructorClient, "ogretmen@hotmail.com", "ogretmen123");
        instructorClient.DefaultRequestHeaders.Add(
            OrganizationHeader, organizationId.ToString());

        var slot = await instructorClient.PostAsJsonAsync(
            new Uri("/api/v1/instructors/me/slots", UriKind.Relative),
            new { courseId, startsAt = DateTimeOffset.UtcNow.AddDays(10) },
            cancellationToken);
        slot.StatusCode.ShouldBe(
            HttpStatusCode.OK,
            await slot.Content.ReadAsStringAsync(cancellationToken));

        var opened = await slot.Content.ReadFromJsonAsync<OpenInstructorSlotResult>(
            TestJson.Options,
            cancellationToken);

        return opened!.SessionId;
    }

    private static async Task<Guid> CreatePublishedPrivateCourse(HttpClient client, Guid subjectId)
    {
        var course = await client.PostAsJsonAsync(
            new Uri("/api/v1/courses", UriKind.Relative),
            new
            {
                subjectId,
                title = "Birebir Ingilizce",
                courseType = "Private",
                defaultDurationMinutes = 50,
                minParticipants = 1,
                maxParticipants = 1
            },
            TestContext.Current.CancellationToken);
        course.StatusCode.ShouldBe(HttpStatusCode.OK);

        var created = await course.Content.ReadFromJsonAsync<CreateCourseResult>(
            TestJson.Options,
            TestContext.Current.CancellationToken);

        (await client.PostAsync(
            new Uri($"/api/v1/courses/{created!.CourseId}/publish", UriKind.Relative),
            content: null,
            TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        return created.CourseId;
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
