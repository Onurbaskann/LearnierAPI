using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Learnier.Application.Features.Authentication.Commands.LoginUser;
using Learnier.Application.Features.Catalog.Commands.CreateCourse;
using Learnier.Application.Features.Catalog.Commands.CreateSubject;
using Learnier.Application.Features.Organizations.Commands.CreateOrganization;
using Learnier.Application.Features.Scheduling.Commands.CreateBooking;
using Learnier.Application.Features.Scheduling.Commands.CreateSession;
using Learnier.Domain.Scheduling;
using Learnier.WebApi.Controllers;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Learnier.IntegrationTests;

/// <summary>
/// Kontenjan yarisinin gercek veritabaninda dogrulanmasi.
/// </summary>
/// <remarks>
/// <para>
/// Kaynak dokumanin 7. bolumu bu senaryoyu ozellikle isaret ediyor: "once say,
/// sonra insert et" yaklasimi es zamanli isteklerde kontenjani asar. Buradaki
/// testler o korumanin gercekten calistigini kanitliyor.
/// </para>
/// <para>
/// Bu testler in-memory saglayiciyla anlamsiz olurdu: satir kilidi yalnizca
/// gercek PostgreSQL'de vardir.
/// </para>
/// </remarks>
public sealed class BookingConcurrencyTests(AuthApiFixture fixture) : IClassFixture<AuthApiFixture>
{
    private const string OrganizationHeader = "X-Organization-Id";

    /// <summary>
    /// Kapasitesi 1 olan oturuma es zamanli 10 rezervasyon: tam olarak 1 Reserved.
    /// </summary>
    [Fact]
    public async Task ConcurrentBookings_DoNotExceedCapacity()
    {
        var setup = await NewSessionWithCapacity(capacity: 1);

        // On bagimsiz istemci, hepsi ayni anda ayni oturuma.
        var learners = await CreateLearners(setup, count: 10);

        var responses = await Task.WhenAll(
            learners.Select(l => l.Client.PostAsJsonAsync(
                new Uri($"/api/v1/sessions/{setup.SessionId}/bookings", UriKind.Relative),
                new CreateBookingRequest(null),
                TestContext.Current.CancellationToken)));

        foreach (var response in responses)
        {
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        var results = await Task.WhenAll(
            responses.Select(r => r.Content.ReadFromJsonAsync<CreateBookingResult>(
                TestJson.Options, TestContext.Current.CancellationToken)));

        var reserved = results.Count(r => r!.Status == BookingStatus.Reserved);
        var waitlisted = results.Count(r => r!.Status == BookingStatus.Waitlisted);

        reserved.ShouldBe(1, "kontenjan 1 iken birden fazla rezervasyon kabul edilmemeli");
        waitlisted.ShouldBe(9);

        // Veritabani da ayni seyi soylemeli: bellekteki sonuc yaniltici olabilir.
        await AssertReservedCount(setup.SessionId, expected: 1);

        foreach (var learner in learners)
        {
            learner.Client.Dispose();
        }

        setup.Dispose();
    }

    /// <summary>
    /// Kapasitesi 3 olan oturuma es zamanli 10 rezervasyon: tam olarak 3 Reserved.
    /// </summary>
    [Fact]
    public async Task ConcurrentBookings_FillExactlyToCapacity()
    {
        var setup = await NewSessionWithCapacity(capacity: 3);
        var learners = await CreateLearners(setup, count: 10);

        await Task.WhenAll(
            learners.Select(l => l.Client.PostAsJsonAsync(
                new Uri($"/api/v1/sessions/{setup.SessionId}/bookings", UriKind.Relative),
                new CreateBookingRequest(null),
                TestContext.Current.CancellationToken)));

        await AssertReservedCount(setup.SessionId, expected: 3);

        foreach (var learner in learners)
        {
            learner.Client.Dispose();
        }

        setup.Dispose();
    }

    /// <summary>
    /// Ayni ogrenci ayni oturuma iki kez rezervasyon yapamaz.
    /// </summary>
    [Fact]
    public async Task DuplicateBooking_IsRejected()
    {
        var setup = await NewSessionWithCapacity(capacity: 5);
        var learner = (await CreateLearners(setup, count: 1))[0];

        var first = await Book(learner.Client, setup.SessionId);
        first.StatusCode.ShouldBe(HttpStatusCode.OK);

        var second = await Book(learner.Client, setup.SessionId);
        second.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        learner.Client.Dispose();
        setup.Dispose();
    }

    /// <summary>
    /// Iptal edilen yer, bekleme listesindeki ilk kayda gecmeli.
    /// </summary>
    [Fact]
    public async Task CancellingReservation_PromotesFirstWaitlisted()
    {
        var setup = await NewSessionWithCapacity(capacity: 1);
        var learners = await CreateLearners(setup, count: 2);

        var firstResponse = await Book(learners[0].Client, setup.SessionId);
        var first = await firstResponse.Content.ReadFromJsonAsync<CreateBookingResult>(
            TestJson.Options, TestContext.Current.CancellationToken);

        first!.Status.ShouldBe(BookingStatus.Reserved);

        var secondResponse = await Book(learners[1].Client, setup.SessionId);
        var second = await secondResponse.Content.ReadFromJsonAsync<CreateBookingResult>(
            TestJson.Options, TestContext.Current.CancellationToken);

        second!.Status.ShouldBe(BookingStatus.Waitlisted);

        var cancel = await learners[0].Client.DeleteAsync(
            new Uri($"/api/v1/bookings/{first.BookingId}", UriKind.Relative),
            TestContext.Current.CancellationToken);

        cancel.StatusCode.ShouldBe(HttpStatusCode.OK);

        var cancelResult = await cancel.Content.ReadFromJsonAsync<CancelBookingResultDto>(
            TestJson.Options, TestContext.Current.CancellationToken);

        cancelResult!.PromotedBookingId.ShouldBe(second.BookingId);

        // Kontenjan yine 1 dolu: bosalan yeri bekleyen aldi.
        await AssertReservedCount(setup.SessionId, expected: 1);

        foreach (var learner in learners)
        {
            learner.Client.Dispose();
        }

        setup.Dispose();
    }

    /// <summary>
    /// Baska bir ogrencinin rezervasyonu, yetkisi olmayan tarafindan iptal edilemez.
    /// </summary>
    [Fact]
    public async Task CancellingAnotherLearnersBooking_IsRejected()
    {
        var setup = await NewSessionWithCapacity(capacity: 5);
        var learners = await CreateLearners(setup, count: 2);

        var response = await Book(learners[0].Client, setup.SessionId);
        var booking = await response.Content.ReadFromJsonAsync<CreateBookingResult>(
            TestJson.Options, TestContext.Current.CancellationToken);

        var cancel = await learners[1].Client.DeleteAsync(
            new Uri($"/api/v1/bookings/{booking!.BookingId}", UriKind.Relative),
            TestContext.Current.CancellationToken);

        cancel.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        foreach (var learner in learners)
        {
            learner.Client.Dispose();
        }

        setup.Dispose();
    }

    private static Task<HttpResponseMessage> Book(HttpClient client, Guid sessionId)
        => client.PostAsJsonAsync(
            new Uri($"/api/v1/sessions/{sessionId}/bookings", UriKind.Relative),
            new CreateBookingRequest(null),
            TestContext.Current.CancellationToken);

    private async Task AssertReservedCount(Guid sessionId, int expected)
    {
        await using var context = fixture.CreateContext();

        var reserved = await context.SessionBookings
            .CountAsync(
                b => b.SessionId == sessionId && b.Status == BookingStatus.Reserved,
                TestContext.Current.CancellationToken);

        reserved.ShouldBe(expected);
    }

    /// <summary>
    /// Verilen kontenjanla rezervasyona acik bir oturum hazirlar.
    /// </summary>
    private async Task<SessionSetup> NewSessionWithCapacity(int capacity)
    {
        var ownerClient = fixture.CreateClient();
        await SignIn(ownerClient, "ogrenci@hotmail.com", "ogrenci123");

        var organization = await ownerClient.PostAsJsonAsync(
            new Uri("/api/v1/organizations", UriKind.Relative),
            new
            {
                name = "Rezervasyon Testi",
                slug = $"rez-{Guid.CreateVersion7():N}"[..20],
                organizationType = "Provider",
                timeZoneId = "Europe/Istanbul",
                defaultCurrency = "TRY"
            },
            TestContext.Current.CancellationToken);

        var created = await organization.Content.ReadFromJsonAsync<CreateOrganizationResult>(
            TestContext.Current.CancellationToken);

        ownerClient.DefaultRequestHeaders.Add(
            OrganizationHeader, created!.OrganizationId.ToString());

        var subject = await ownerClient.PostAsJsonAsync(
            new Uri("/api/v1/subjects", UriKind.Relative),
            new CreateSubjectCommand("Yazilim", $"alan-{Guid.CreateVersion7():N}"[..20], null),
            TestContext.Current.CancellationToken);

        var subjectId = (await subject.Content.ReadFromJsonAsync<CreateSubjectResult>(
            TestContext.Current.CancellationToken))!.SubjectId;

        var course = await ownerClient.PostAsJsonAsync(
            new Uri("/api/v1/courses", UriKind.Relative),
            new
            {
                subjectId,
                title = "Konusma Kulubu",
                courseType = "DropIn",
                defaultDurationMinutes = 45,
                minParticipants = 1,
                maxParticipants = 20
            },
            TestContext.Current.CancellationToken);

        var courseId = (await course.Content.ReadFromJsonAsync<CreateCourseResult>(
            TestJson.Options, TestContext.Current.CancellationToken))!.CourseId;

        var startsAt = DateTimeOffset.UtcNow.AddDays(7);

        var session = await ownerClient.PostAsJsonAsync(
            new Uri("/api/v1/sessions", UriKind.Relative),
            new
            {
                courseId,
                sessionType = "Group",
                startsAt,
                endsAt = startsAt.AddMinutes(45),
                capacity,
                minimumParticipants = 1
            },
            TestContext.Current.CancellationToken);

        session.StatusCode.ShouldBe(HttpStatusCode.OK);

        var sessionId = (await session.Content.ReadFromJsonAsync<CreateSessionResult>(
            TestJson.Options, TestContext.Current.CancellationToken))!.SessionId;

        return new SessionSetup(ownerClient, created.OrganizationId, sessionId);
    }

    /// <summary>
    /// Kuruma uye, oturum acmis ve rezervasyon yapabilen ogrenciler uretir.
    /// </summary>
    private async Task<IReadOnlyList<Learner>> CreateLearners(SessionSetup setup, int count)
    {
        var studentRoleId = await SystemRoleId("student");
        var learners = new List<Learner>(count);

        for (var i = 0; i < count; i++)
        {
            var email = $"ogrenci-{Guid.CreateVersion7():N}@ornek.com";

            await RegisterAndVerify(email);

            var invited = await setup.OwnerClient.PostAsJsonAsync(
                new Uri("/api/v1/organizations/members", UriKind.Relative),
                new { email, roleId = studentRoleId },
                TestContext.Current.CancellationToken);

            invited.StatusCode.ShouldBe(HttpStatusCode.OK);

            await ActivateMembership(setup.OrganizationId, email);

            var client = fixture.CreateClient();
            await SignIn(client, email, "CokGuvenli123");
            client.DefaultRequestHeaders.Add(
                OrganizationHeader, setup.OrganizationId.ToString());

            learners.Add(new Learner(email, client));
        }

        return learners;
    }

    private async Task RegisterAndVerify(string email)
    {
        using var client = fixture.CreateClient();

        var registered = await client.PostAsJsonAsync(
            new Uri("/api/v1/auth/register", UriKind.Relative),
            new { email, password = "CokGuvenli123", firstName = "Ogrenci", lastName = "Test" },
            TestContext.Current.CancellationToken);

        registered.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Dogrulama ucu yerine dogrudan onaylaniyor: token ham hali yalnizca
        // e-posta gondericisine veriliyor ve testten okunamiyor.
        await using var context = fixture.CreateContext();

        var user = await context.Users.FirstAsync(
            u => u.Email == email, TestContext.Current.CancellationToken);

        user.ConfirmEmail(DateTimeOffset.UtcNow);

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
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

    private sealed record Learner(string Email, HttpClient Client);

    private sealed record SessionSetup(HttpClient OwnerClient, Guid OrganizationId, Guid SessionId)
    {
        public void Dispose() => OwnerClient.Dispose();
    }

    /// <summary>Iptal yanitini okumak icin; kayitlar Application katmaninda internal degil.</summary>
    private sealed record CancelBookingResultDto(bool Refunded, Guid? PromotedBookingId);
}
