using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Learnier.Application.Common.Models;
using Learnier.Application.Features.Authentication.Commands.LoginUser;
using Learnier.Application.Features.Catalog.Commands.CreateSubject;
using Learnier.Application.Features.Organizations.Commands.CreateOrganization;
using Learnier.Application.Features.Teaching.Commands.CreateInstructorProfile;
using Learnier.Application.Features.Teaching.Queries;
using Learnier.Domain.Identity;
using Learnier.Domain.Teaching;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Learnier.IntegrationTests;

/// <summary>
/// Egitmen profili, yetkinlik ve uygunluk takvimi akisi.
/// </summary>
/// <remarks>
/// Her test kendi kurumunu kurar; kurucu sahip rolu sayesinde egitmenleri
/// yonetebilir. Egitmenin kendi profiline erisimi ayri bir istemciyle denenir.
/// </remarks>
public sealed class InstructorEndpointTests(AuthApiFixture fixture) : IClassFixture<AuthApiFixture>
{
    private const string OrganizationHeader = "X-Organization-Id";

    [Fact]
    public async Task InstructorProfile_CanBeBuiltEndToEnd()
    {
        var context = await NewOrganization();
        var subjectId = await CreateSubject(context.OwnerClient, "Ingilizce");

        var profileId = await CreateProfile(context.OwnerClient, context.InstructorMembershipId);

        // Profil onay bekler durumda baslar.
        var afterCreate = await GetDetail(context.OwnerClient, profileId);
        afterCreate.Status.ShouldBe(InstructorStatus.Pending);

        var activate = await context.OwnerClient.PostAsync(
            new Uri($"/api/v1/instructors/{profileId}/activate", UriKind.Relative),
            content: null,
            TestContext.Current.CancellationToken);

        activate.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var subject = await context.OwnerClient.PostAsJsonAsync(
            new Uri($"/api/v1/instructors/{profileId}/subjects", UriKind.Relative),
            new { subjectId, levelId = (Guid?)null },
            TestContext.Current.CancellationToken);

        subject.StatusCode.ShouldBe(HttpStatusCode.OK);

        var availability = await AddAvailability(
            context.OwnerClient, profileId, DayOfWeek.Monday, "09:00:00", "12:00:00");

        availability.StatusCode.ShouldBe(HttpStatusCode.OK);

        var detail = await GetDetail(context.OwnerClient, profileId);

        detail.Status.ShouldBe(InstructorStatus.Active);
        detail.Subjects.ShouldHaveSingleItem().SubjectName.ShouldBe("Ingilizce");

        var slot = detail.Availabilities.ShouldHaveSingleItem();
        slot.DayOfWeek.ShouldBe(DayOfWeek.Monday);
        slot.StartLocalTime.ShouldBe(new TimeOnly(9, 0));

        context.Dispose();
    }

    /// <summary>
    /// Cakisan uygunluk araligi reddedilmeli.
    /// </summary>
    /// <remarks>
    /// Cakisma kabul edilseydi slot uretiminde ayni saat iki kez uretilir ve
    /// egitmen ayni anda iki oturuma atanabilirdi.
    /// </remarks>
    [Fact]
    public async Task OverlappingAvailability_IsRejected()
    {
        var context = await NewOrganization();
        var profileId = await CreateProfile(context.OwnerClient, context.InstructorMembershipId);

        (await AddAvailability(context.OwnerClient, profileId, DayOfWeek.Tuesday, "09:00:00", "12:00:00"))
            .StatusCode.ShouldBe(HttpStatusCode.OK);

        // 11:00-13:00 mevcut 09:00-12:00 ile kesisiyor.
        var overlapping = await AddAvailability(
            context.OwnerClient, profileId, DayOfWeek.Tuesday, "11:00:00", "13:00:00");

        overlapping.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        var problem = await overlapping.Content.ReadFromJsonAsync<JsonElement>(
            TestContext.Current.CancellationToken);

        problem.GetProperty("errorCode").GetString().ShouldBe("teaching.availability_overlaps");

        context.Dispose();
    }

    /// <summary>
    /// Bitisi baslangica esit olan araliklar cakisma sayilmaz.
    /// </summary>
    [Fact]
    public async Task AdjacentAvailability_IsAccepted()
    {
        var context = await NewOrganization();
        var profileId = await CreateProfile(context.OwnerClient, context.InstructorMembershipId);

        (await AddAvailability(context.OwnerClient, profileId, DayOfWeek.Wednesday, "09:00:00", "12:00:00"))
            .StatusCode.ShouldBe(HttpStatusCode.OK);

        (await AddAvailability(context.OwnerClient, profileId, DayOfWeek.Wednesday, "12:00:00", "15:00:00"))
            .StatusCode.ShouldBe(HttpStatusCode.OK);

        var detail = await GetDetail(context.OwnerClient, profileId);
        detail.Availabilities.Count.ShouldBe(2);

        context.Dispose();
    }

    /// <summary>
    /// Ayni gunde olsa da gecerlilik pencereleri kesismeyen araliklar cakismaz.
    /// </summary>
    [Fact]
    public async Task SameHoursInDifferentValidityWindows_AreAccepted()
    {
        var context = await NewOrganization();
        var profileId = await CreateProfile(context.OwnerClient, context.InstructorMembershipId);

        var first = await context.OwnerClient.PostAsJsonAsync(
            new Uri($"/api/v1/instructors/{profileId}/availabilities", UriKind.Relative),
            new
            {
                dayOfWeek = "Thursday",
                startLocalTime = "09:00:00",
                endLocalTime = "12:00:00",
                validFrom = "2026-01-01",
                validUntil = "2026-06-30"
            },
            TestContext.Current.CancellationToken);

        first.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Ayni saatler ama onceki pencere kapandiktan sonra.
        var second = await context.OwnerClient.PostAsJsonAsync(
            new Uri($"/api/v1/instructors/{profileId}/availabilities", UriKind.Relative),
            new
            {
                dayOfWeek = "Thursday",
                startLocalTime = "09:00:00",
                endLocalTime = "12:00:00",
                validFrom = "2026-07-01",
                validUntil = (string?)null
            },
            TestContext.Current.CancellationToken);

        second.StatusCode.ShouldBe(HttpStatusCode.OK);

        context.Dispose();
    }

    /// <summary>
    /// Egitmen kendi programini yonetebilmeli.
    /// </summary>
    [Fact]
    public async Task Instructor_CanManageOwnAvailability()
    {
        var context = await NewOrganization();
        var profileId = await CreateProfile(context.OwnerClient, context.InstructorMembershipId);

        var instructorClient = await context.SignInAsInstructor();

        var response = await AddAvailability(
            instructorClient, profileId, DayOfWeek.Friday, "14:00:00", "17:00:00");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        instructorClient.Dispose();
        context.Dispose();
    }

    /// <summary>
    /// Baskasinin profiline, yonetici olmayan bir uye yazamamali.
    /// </summary>
    [Fact]
    public async Task NonOwner_CannotManageAnotherProfile()
    {
        var context = await NewOrganization();

        // Sahip kendi uyeligine de profil acar; egitmen ona yazmaya calisacak.
        var ownerProfileId = await CreateProfile(context.OwnerClient, context.OwnerMembershipId);

        var instructorClient = await context.SignInAsInstructor();

        var response = await AddAvailability(
            instructorClient, ownerProfileId, DayOfWeek.Saturday, "10:00:00", "11:00:00");

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(
            TestContext.Current.CancellationToken);

        problem.GetProperty("errorCode").GetString().ShouldBe("teaching.profile_not_owned");

        instructorClient.Dispose();
        context.Dispose();
    }

    /// <summary>
    /// Bir uyelik icin ikinci profil acilamaz.
    /// </summary>
    [Fact]
    public async Task DuplicateProfile_IsRejected()
    {
        var context = await NewOrganization();

        await CreateProfile(context.OwnerClient, context.InstructorMembershipId);

        var second = await context.OwnerClient.PostAsJsonAsync(
            new Uri("/api/v1/instructors", UriKind.Relative),
            new CreateInstructorProfileCommand(context.InstructorMembershipId, "Europe/Istanbul"),
            TestContext.Current.CancellationToken);

        second.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        context.Dispose();
    }

    /// <summary>
    /// Egitmen listesi alana gore filtrelenebilmeli.
    /// </summary>
    [Fact]
    public async Task InstructorList_CanBeFilteredBySubject()
    {
        var context = await NewOrganization();

        var englishId = await CreateSubject(context.OwnerClient, "Ingilizce");
        var mathId = await CreateSubject(context.OwnerClient, "Matematik");

        var profileId = await CreateProfile(context.OwnerClient, context.InstructorMembershipId);

        await context.OwnerClient.PostAsJsonAsync(
            new Uri($"/api/v1/instructors/{profileId}/subjects", UriKind.Relative),
            new { subjectId = englishId, levelId = (Guid?)null },
            TestContext.Current.CancellationToken);

        var matching = await context.OwnerClient.GetFromJsonAsync<PagedResult<InstructorListItem>>(
            new Uri($"/api/v1/instructors?subjectId={englishId}", UriKind.Relative),
            TestJson.Options,
            TestContext.Current.CancellationToken);

        matching!.Items.Select(i => i.Id).ShouldContain(profileId);

        var other = await context.OwnerClient.GetFromJsonAsync<PagedResult<InstructorListItem>>(
            new Uri($"/api/v1/instructors?subjectId={mathId}", UriKind.Relative),
            TestJson.Options,
            TestContext.Current.CancellationToken);

        other!.Items.Select(i => i.Id).ShouldNotContain(profileId);

        context.Dispose();
    }

    [Fact]
    public async Task AvailabilityOverride_CanBeAddedAndListed()
    {
        var context = await NewOrganization();
        var profileId = await CreateProfile(context.OwnerClient, context.InstructorMembershipId);

        var response = await context.OwnerClient.PostAsJsonAsync(
            new Uri($"/api/v1/instructors/{profileId}/availability-overrides", UriKind.Relative),
            new
            {
                overrideDate = "2026-12-31",
                overrideType = "Unavailable",
                startLocalTime = (string?)null,
                endLocalTime = (string?)null,
                reason = "Yilbasi tatili"
            },
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var overrides = await context.OwnerClient
            .GetFromJsonAsync<IReadOnlyList<AvailabilityOverrideDetail>>(
                new Uri($"/api/v1/instructors/{profileId}/availability-overrides?from=2026-01-01", UriKind.Relative),
                TestJson.Options,
                TestContext.Current.CancellationToken);

        var single = overrides.ShouldHaveSingleItem();
        single.OverrideType.ShouldBe(AvailabilityOverrideType.Unavailable);
        single.Reason.ShouldBe("Yilbasi tatili");

        context.Dispose();
    }

    private static Task<HttpResponseMessage> AddAvailability(
        HttpClient client,
        Guid profileId,
        DayOfWeek dayOfWeek,
        string start,
        string end)
        => client.PostAsJsonAsync(
            new Uri($"/api/v1/instructors/{profileId}/availabilities", UriKind.Relative),
            new
            {
                dayOfWeek = dayOfWeek.ToString(),
                startLocalTime = start,
                endLocalTime = end,
                validFrom = "2026-01-01",
                validUntil = (string?)null
            },
            TestContext.Current.CancellationToken);

    private static async Task<InstructorDetail> GetDetail(HttpClient client, Guid profileId)
    {
        var detail = await client.GetFromJsonAsync<InstructorDetail>(
            new Uri($"/api/v1/instructors/{profileId}", UriKind.Relative),
            TestJson.Options,
            TestContext.Current.CancellationToken);

        detail.ShouldNotBeNull();

        return detail;
    }

    private static async Task<Guid> CreateProfile(HttpClient client, Guid membershipId)
    {
        var response = await client.PostAsJsonAsync(
            new Uri("/api/v1/instructors", UriKind.Relative),
            new CreateInstructorProfileCommand(membershipId, "Europe/Istanbul", "Deneyimli egitmen"),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var created = await response.Content.ReadFromJsonAsync<CreateInstructorProfileResult>(
            TestJson.Options, TestContext.Current.CancellationToken);

        return created!.ProfileId;
    }

    private static async Task<Guid> CreateSubject(HttpClient client, string name)
    {
        var response = await client.PostAsJsonAsync(
            new Uri("/api/v1/subjects", UriKind.Relative),
            new CreateSubjectCommand(name, $"alan-{Guid.CreateVersion7():N}"[..20], null),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var created = await response.Content.ReadFromJsonAsync<CreateSubjectResult>(
            TestContext.Current.CancellationToken);

        return created!.SubjectId;
    }

    /// <summary>
    /// Yeni bir kurum, sahibi ve icinde aktif bir egitmen uyeligi hazirlar.
    /// </summary>
    private async Task<OrganizationContext> NewOrganization()
    {
        var ownerClient = fixture.CreateClient();
        await SignIn(ownerClient, "ogrenci@hotmail.com", "ogrenci123");

        var response = await ownerClient.PostAsJsonAsync(
            new Uri("/api/v1/organizations", UriKind.Relative),
            new
            {
                name = "Egitmen Testi",
                slug = $"egitmen-{Guid.CreateVersion7():N}"[..24],
                organizationType = "Provider",
                timeZoneId = "Europe/Istanbul",
                defaultCurrency = "TRY"
            },
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var organization = await response.Content.ReadFromJsonAsync<CreateOrganizationResult>(
            TestContext.Current.CancellationToken);

        ownerClient.DefaultRequestHeaders.Add(
            OrganizationHeader, organization!.OrganizationId.ToString());

        var instructorRoleId = await SystemRoleId("instructor");

        var invited = await ownerClient.PostAsJsonAsync(
            new Uri("/api/v1/organizations/members", UriKind.Relative),
            new { email = "ogretmen@hotmail.com", roleId = instructorRoleId },
            TestContext.Current.CancellationToken);

        invited.StatusCode.ShouldBe(HttpStatusCode.OK);

        var instructorMembershipId = await ActivateInvitedMembership(organization.OrganizationId);

        return new OrganizationContext(
            fixture,
            ownerClient,
            organization.OrganizationId,
            organization.MembershipId,
            instructorMembershipId);
    }

    /// <summary>
    /// Davet edilen uyeligi aktiflestirir ve kimligini dondurur.
    /// </summary>
    /// <remarks>
    /// Daveti kabul etme ucu henuz yok; kiraci cozumlemesinin gecmesi icin
    /// uyelik dogrudan aktiflestiriliyor.
    /// </remarks>
    private async Task<Guid> ActivateInvitedMembership(Guid organizationId)
    {
        await using var context = fixture.CreateContext();

        var membership = await context.Memberships
            .Where(m => m.OrganizationId == organizationId && m.Status == MembershipStatus.Invited)
            .FirstAsync(TestContext.Current.CancellationToken);

        membership.Accept(DateTimeOffset.UtcNow);

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return membership.Id;
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

    private sealed record OrganizationContext(
        AuthApiFixture Fixture,
        HttpClient OwnerClient,
        Guid OrganizationId,
        Guid OwnerMembershipId,
        Guid InstructorMembershipId)
    {
        /// <summary>Egitmen olarak oturum acmis yeni bir istemci dondurur.</summary>
        public async Task<HttpClient> SignInAsInstructor()
        {
            var client = Fixture.CreateClient();
            await SignIn(client, "ogretmen@hotmail.com", "ogretmen123");
            client.DefaultRequestHeaders.Add(OrganizationHeader, OrganizationId.ToString());

            return client;
        }

        public void Dispose() => OwnerClient.Dispose();
    }
}
