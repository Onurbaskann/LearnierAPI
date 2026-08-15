using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Learnier.Application.Features.Authentication.Commands.LoginUser;
using Learnier.Application.Features.Billing.Queries;
using Learnier.Application.Features.Catalog.Commands.CreateCourse;
using Learnier.Application.Features.Catalog.Commands.CreateSubject;
using Learnier.Application.Features.Organizations.Commands.CreateOrganization;
using Learnier.Application.Features.Scheduling.Commands.CreateSession;
using Learnier.Domain.Billing;
using Learnier.Domain.Identity;
using Learnier.Domain.Scheduling;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Learnier.IntegrationTests;

/// <summary>
/// Abonelik, ders hakki ve rezervasyon arasindaki bag.
/// </summary>
/// <remarks>
/// Kaynak dokumanin 9. bolumunun iki kurali burada dogrulaniyor: kalan hak
/// defterden hesaplanir (saklanan sayac yok) ve iade, harcamayi duzeltmek yerine
/// ters yonlu yeni bir hareketle yazilir.
/// </remarks>
public sealed class SubscriptionBookingTests(AuthApiFixture fixture) : IClassFixture<AuthApiFixture>
{
    private const string OrganizationHeader = "X-Organization-Id";
    private const string Password = "CokGuvenli123";

    /// <summary>
    /// Kredi ile rezervasyon bakiyeyi dusurur; iptal geri yukler.
    /// </summary>
    [Fact]
    public async Task CreditBooking_DecreasesBalance_AndCancellationRestoresIt()
    {
        var env = await NewEnvironment(creditQuantity: 3);

        (await Balance(env)).ShouldBe(3);

        var booking = await Book(env);
        booking.StatusCode.ShouldBe(HttpStatusCode.OK);

        var created = await booking.Content.ReadFromJsonAsync<JsonElement>(
            TestContext.Current.CancellationToken);

        created.GetProperty("accessSource").GetString().ShouldBe("Credit");

        (await Balance(env)).ShouldBe(2);

        var bookingId = created.GetProperty("bookingId").GetGuid();

        var cancelled = await env.LearnerClient.DeleteAsync(
            new Uri($"/api/v1/bookings/{bookingId}", UriKind.Relative),
            TestContext.Current.CancellationToken);

        cancelled.StatusCode.ShouldBe(HttpStatusCode.OK);

        (await Balance(env)).ShouldBe(3);

        // Iade, harcamayi silmez: defterde iki hareket birden durur.
        await using var context = fixture.CreateContext();

        var entries = await context.CreditLedger
            .Where(e => e.SubscriptionId == env.SubscriptionId)
            .OrderBy(e => e.CreatedAt)
            .ToListAsync(TestContext.Current.CancellationToken);

        entries.Select(e => e.TransactionType).ShouldBe(
        [
            CreditTransactionType.PeriodGrant,
            CreditTransactionType.BookingUsage,
            CreditTransactionType.CancellationRefund
        ]);

        env.Dispose();
    }

    /// <summary>
    /// Bakiye bittiginde rezervasyon reddedilir.
    /// </summary>
    [Fact]
    public async Task BookingWithoutCredit_IsRejected()
    {
        var env = await NewEnvironment(creditQuantity: 1);

        (await Book(env)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await Balance(env)).ShouldBe(0);

        // Ikinci oturum: hak kalmadi.
        var secondSession = await CreateSession(env);

        var response = await Book(env, secondSession);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(
            TestContext.Current.CancellationToken);

        problem.GetProperty("errorCode").GetString().ShouldBe("billing.insufficient_credit");

        env.Dispose();
    }

    /// <summary>
    /// Plan kapsami disindaki egitime rezervasyon yapilamaz.
    /// </summary>
    [Fact]
    public async Task BookingCourseOutsidePlanScope_IsRejected()
    {
        var env = await NewEnvironment(creditQuantity: 5, restrictToOwnCourse: true);

        // Plan yalnizca ilk egitimi kapsiyor; ikinci egitim kapsam disi.
        var otherCourseId = await CreateCourse(env.OwnerClient, env.SubjectId, "Kapsam Disi");
        var otherSession = await CreateSession(env, otherCourseId);

        var response = await Book(env, otherSession);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(
            TestContext.Current.CancellationToken);

        problem.GetProperty("errorCode").GetString().ShouldBe("billing.course_not_covered");

        env.Dispose();
    }

    /// <summary>
    /// Es zamanli rezervasyonlar bakiyeyi eksiye dusurmemeli.
    /// </summary>
    /// <remarks>
    /// Farkli oturumlara ayni anda yapilan rezervasyonlar oturum kilidini
    /// paylasmaz; bakiyeyi koruyan sey abonelik satirinin kilitlenmesidir.
    /// </remarks>
    [Fact]
    public async Task ConcurrentCreditBookings_DoNotOverdraw()
    {
        var env = await NewEnvironment(creditQuantity: 2);

        // Bes ayri oturum: hepsi ayni ogrencinin ayni bakiyesinden harcayacak.
        var sessions = new List<Guid>();
        for (var i = 0; i < 5; i++)
        {
            sessions.Add(await CreateSession(env));
        }

        await Task.WhenAll(sessions.Select(s => Book(env, s)));

        var balance = await Balance(env);

        // Bakiye asla eksiye dusmemeli.
        balance.ShouldBeGreaterThanOrEqualTo(0);
        balance.ShouldBe(0, "iki kredi harcanmali, fazlasi reddedilmeli");

        await using var context = fixture.CreateContext();

        var usageCount = await context.CreditLedger
            .CountAsync(
                e => e.SubscriptionId == env.SubscriptionId
                     && e.TransactionType == CreditTransactionType.BookingUsage,
                TestContext.Current.CancellationToken);

        usageCount.ShouldBe(2);

        env.Dispose();
    }

    /// <summary>
    /// Fiyat degistiginde eski kayit arsivlenir, guncellenmez.
    /// </summary>
    [Fact]
    public async Task PlanPrice_IsVersionedNotUpdated()
    {
        var env = await NewEnvironment(creditQuantity: 1);

        var updated = await env.OwnerClient.PostAsJsonAsync(
            new Uri($"/api/v1/plans/{env.PlanId}/prices", UriKind.Relative),
            new { currency = "TRY", amount = 750m, billingInterval = "Month", billingIntervalCount = 1 },
            TestContext.Current.CancellationToken);

        updated.StatusCode.ShouldBe(HttpStatusCode.OK);

        var result = await updated.Content.ReadFromJsonAsync<JsonElement>(
            TestContext.Current.CancellationToken);

        // Eski fiyat kapatildi, kimligi yanitta bildiriliyor.
        result.GetProperty("archivedPriceId").GetGuid().ShouldBe(env.PlanPriceId);

        await using var context = fixture.CreateContext();

        var prices = await context.PlanPrices
            .Where(p => p.PlanId == env.PlanId)
            .OrderBy(p => p.ValidFrom)
            .ToListAsync(TestContext.Current.CancellationToken);

        // Iki kayit durur: eski tutar silinmedi.
        prices.Count.ShouldBe(2);
        prices[0].Amount.ShouldBe(500m);
        prices[0].Status.ShouldBe(PlanPriceStatus.Archived);
        prices[1].Amount.ShouldBe(750m);
        prices[1].Status.ShouldBe(PlanPriceStatus.Active);

        env.Dispose();
    }

    /// <summary>
    /// Arsivlenmis fiyattan yeni abonelik satilamaz.
    /// </summary>
    [Fact]
    public async Task ArchivedPrice_CannotStartNewSubscription()
    {
        var env = await NewEnvironment(creditQuantity: 1);

        await env.OwnerClient.PostAsJsonAsync(
            new Uri($"/api/v1/plans/{env.PlanId}/prices", UriKind.Relative),
            new { currency = "TRY", amount = 900m, billingInterval = "Month", billingIntervalCount = 1 },
            TestContext.Current.CancellationToken);

        var response = await env.OwnerClient.PostAsJsonAsync(
            new Uri("/api/v1/subscriptions", UriKind.Relative),
            new { planPriceId = env.PlanPriceId, subscriberUserId = env.LearnerUserId },
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(
            TestContext.Current.CancellationToken);

        problem.GetProperty("errorCode").GetString().ShouldBe("billing.plan_price_not_active");

        env.Dispose();
    }

    private static Task<HttpResponseMessage> Book(Environment env, Guid? sessionId = null)
        => env.LearnerClient.PostAsJsonAsync(
            new Uri($"/api/v1/sessions/{sessionId ?? env.SessionId}/bookings", UriKind.Relative),
            new { learnerUserId = (Guid?)null },
            TestContext.Current.CancellationToken);

    /// <summary>Ogrencinin birebir ders bakiyesi.</summary>
    private static async Task<int> Balance(Environment env)
    {
        var balances = await env.LearnerClient.GetFromJsonAsync<IReadOnlyList<CreditBalanceItem>>(
            new Uri("/api/v1/credits/balance", UriKind.Relative),
            TestJson.Options,
            TestContext.Current.CancellationToken);

        return balances!
            .Where(b => b.SessionType == SessionType.Private)
            .Sum(b => b.Balance);
    }

    private static async Task<Guid> CreateCourse(HttpClient client, Guid subjectId, string title)
    {
        var response = await client.PostAsJsonAsync(
            new Uri("/api/v1/courses", UriKind.Relative),
            new
            {
                subjectId,
                title,
                courseType = "Private",
                defaultDurationMinutes = 45,
                minParticipants = 1,
                maxParticipants = 1
            },
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var created = await response.Content.ReadFromJsonAsync<CreateCourseResult>(
            TestJson.Options, TestContext.Current.CancellationToken);

        return created!.CourseId;
    }

    private static async Task<Guid> CreateSession(Environment env, Guid? courseId = null)
    {
        var startsAt = DateTimeOffset.UtcNow.AddDays(7).AddMinutes(Random.Shared.Next(1, 100000));

        var response = await env.OwnerClient.PostAsJsonAsync(
            new Uri("/api/v1/sessions", UriKind.Relative),
            new
            {
                courseId = courseId ?? env.CourseId,
                sessionType = "Private",
                startsAt,
                endsAt = startsAt.AddMinutes(45),
                capacity = 1,
                minimumParticipants = 1
            },
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var created = await response.Content.ReadFromJsonAsync<CreateSessionResult>(
            TestJson.Options, TestContext.Current.CancellationToken);

        return created!.SessionId;
    }

    /// <summary>
    /// Kurum, plan, abonelik ve rezervasyona hazir bir ogrenci kurar.
    /// </summary>
    /// <param name="restrictToOwnCourse">
    /// Dogruysa plan yalnizca olusturulan egitimi kapsar; kapsam disi senaryosu icin.
    /// </param>
    private async Task<Environment> NewEnvironment(int creditQuantity, bool restrictToOwnCourse = false)
    {
        var ownerClient = fixture.CreateClient();
        await SignIn(ownerClient, "ogrenci@hotmail.com", "ogrenci123");

        var organization = await ownerClient.PostAsJsonAsync(
            new Uri("/api/v1/organizations", UriKind.Relative),
            new
            {
                name = "Abonelik Testi",
                slug = $"abn-{Guid.CreateVersion7():N}"[..20],
                organizationType = "Provider",
                timeZoneId = "Europe/Istanbul",
                defaultCurrency = "TRY"
            },
            TestContext.Current.CancellationToken);

        var created = await organization.Content.ReadFromJsonAsync<CreateOrganizationResult>(
            TestContext.Current.CancellationToken);

        var organizationId = created!.OrganizationId;
        ownerClient.DefaultRequestHeaders.Add(OrganizationHeader, organizationId.ToString());

        var subject = await ownerClient.PostAsJsonAsync(
            new Uri("/api/v1/subjects", UriKind.Relative),
            new CreateSubjectCommand("Ingilizce", $"alan-{Guid.CreateVersion7():N}"[..20], null),
            TestContext.Current.CancellationToken);

        var subjectId = (await subject.Content.ReadFromJsonAsync<CreateSubjectResult>(
            TestContext.Current.CancellationToken))!.SubjectId;

        var courseId = await CreateCourse(ownerClient, subjectId, "Birebir Konusma");

        // Plan: sayili birebir ders hakki.
        var plan = await ownerClient.PostAsJsonAsync(
            new Uri("/api/v1/plans", UriKind.Relative),
            new
            {
                name = "Birebir Paket",
                catalogAccess = restrictToOwnCourse ? "Restricted" : "All",
                description = (string?)null
            },
            TestContext.Current.CancellationToken);

        var planId = (await plan.Content.ReadFromJsonAsync<JsonElement>(
            TestContext.Current.CancellationToken)).GetProperty("planId").GetGuid();

        if (restrictToOwnCourse)
        {
            var access = await ownerClient.PostAsJsonAsync(
                new Uri($"/api/v1/plans/{planId}/access", UriKind.Relative),
                new { subjectId = (Guid?)null, courseId },
                TestContext.Current.CancellationToken);

            access.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        }

        var price = await ownerClient.PostAsJsonAsync(
            new Uri($"/api/v1/plans/{planId}/prices", UriKind.Relative),
            new { currency = "TRY", amount = 500m, billingInterval = "Month", billingIntervalCount = 1 },
            TestContext.Current.CancellationToken);

        var planPriceId = (await price.Content.ReadFromJsonAsync<JsonElement>(
            TestContext.Current.CancellationToken)).GetProperty("planPriceId").GetGuid();

        var entitlement = await ownerClient.PostAsJsonAsync(
            new Uri($"/api/v1/plans/{planId}/entitlements", UriKind.Relative),
            new
            {
                entitlementType = "LessonCredit",
                sessionType = "Private",
                resetPeriod = "Month",
                quantity = creditQuantity
            },
            TestContext.Current.CancellationToken);

        entitlement.StatusCode.ShouldBe(HttpStatusCode.OK);

        var activated = await ownerClient.PostAsync(
            new Uri($"/api/v1/plans/{planId}/activate", UriKind.Relative),
            content: null,
            TestContext.Current.CancellationToken);

        activated.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // Ogrenci: kayit, dogrulama, uyelik, abonelik.
        var email = $"abone-{Guid.CreateVersion7():N}@ornek.com";
        var learnerUserId = await RegisterAndVerify(email);

        var studentRoleId = await SystemRoleId("student");

        await ownerClient.PostAsJsonAsync(
            new Uri("/api/v1/organizations/members", UriKind.Relative),
            new { email, roleId = studentRoleId },
            TestContext.Current.CancellationToken);

        await ActivateMembership(organizationId, email);

        var subscription = await ownerClient.PostAsJsonAsync(
            new Uri("/api/v1/subscriptions", UriKind.Relative),
            new { planPriceId, subscriberUserId = learnerUserId },
            TestContext.Current.CancellationToken);

        subscription.StatusCode.ShouldBe(HttpStatusCode.OK);

        var subscriptionId = (await subscription.Content.ReadFromJsonAsync<JsonElement>(
            TestContext.Current.CancellationToken)).GetProperty("subscriptionId").GetGuid();

        var learnerClient = fixture.CreateClient();
        await SignIn(learnerClient, email, Password);
        learnerClient.DefaultRequestHeaders.Add(OrganizationHeader, organizationId.ToString());

        var env = new Environment(
            ownerClient, learnerClient, organizationId, subjectId, courseId,
            planId, planPriceId, subscriptionId, learnerUserId, Guid.Empty);

        return env with { SessionId = await CreateSession(env) };
    }

    private async Task<Guid> RegisterAndVerify(string email)
    {
        using var client = fixture.CreateClient();

        var registered = await client.PostAsJsonAsync(
            new Uri("/api/v1/auth/register", UriKind.Relative),
            new { email, password = Password, firstName = "Abone", lastName = "Test" },
            TestContext.Current.CancellationToken);

        registered.StatusCode.ShouldBe(HttpStatusCode.OK);

        await using var context = fixture.CreateContext();

        var user = await context.Users.FirstAsync(
            u => u.Email == email, TestContext.Current.CancellationToken);

        user.ConfirmEmail(DateTimeOffset.UtcNow);

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return user.Id;
    }

    private async Task ActivateMembership(Guid organizationId, string email)
    {
        await using var context = fixture.CreateContext();

        var membership = await context.Memberships
            .Where(m => m.OrganizationId == organizationId && m.User.Email == email)
            .FirstAsync(TestContext.Current.CancellationToken);

        membership.Accept(DateTimeOffset.UtcNow);

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task<Guid> SystemRoleId(string code)
    {
        await using var context = fixture.CreateContext();

        var role = await context.Roles
            .Where(r => r.Code == code && r.OrganizationId == null)
            .FirstAsync(TestContext.Current.CancellationToken);

        return role.Id;
    }

    private static async Task SignIn(HttpClient client, string email, string password)
    {
        var response = await client.PostAsJsonAsync(
            new Uri("/api/v1/auth/login", UriKind.Relative),
            new LoginUserCommand(email, password),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var session = await response.Content.ReadFromJsonAsync<LoginUserResult>(
            TestContext.Current.CancellationToken);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", session!.AccessToken);
    }

    private sealed record Environment(
        HttpClient OwnerClient,
        HttpClient LearnerClient,
        Guid OrganizationId,
        Guid SubjectId,
        Guid CourseId,
        Guid PlanId,
        Guid PlanPriceId,
        Guid SubscriptionId,
        Guid LearnerUserId,
        Guid SessionId)
    {
        public void Dispose()
        {
            OwnerClient.Dispose();
            LearnerClient.Dispose();
        }
    }
}
