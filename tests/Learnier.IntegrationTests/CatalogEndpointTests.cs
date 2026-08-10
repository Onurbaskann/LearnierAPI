using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Learnier.Application.Common.Models;
using Learnier.Application.Features.Authentication.Commands.LoginUser;
using Learnier.Application.Features.Catalog.Commands.CreateCourse;
using Learnier.Application.Features.Catalog.Commands.CreateLevel;
using Learnier.Application.Features.Catalog.Commands.CreateSubject;
using Learnier.Application.Features.Catalog.Queries;
using Learnier.Application.Features.Organizations.Commands.CreateOrganization;
using Shouldly;

namespace Learnier.IntegrationTests;

/// <summary>
/// Katalog akisi: alan, seviye, egitim ve mufredat.
/// </summary>
/// <remarks>
/// Testler kendi organizasyonlarini kurar. Sebep: tohumlanan hesaplarin hicbiri
/// <c>course.manage</c> tasimiyor, kurucu ise sahip rolu sayesinde tasiyor.
/// Ayrica her testin kendi kurumunda calismasi, kiraci izolasyonunu da dogrulanabilir kilar.
/// </remarks>
public sealed class CatalogEndpointTests(AuthApiFixture fixture) : IClassFixture<AuthApiFixture>
{
    private const string OrganizationHeader = "X-Organization-Id";

    [Fact]
    public async Task Catalog_CanBeBuiltEndToEnd()
    {
        var (client, _) = await NewOrganizationClient();

        var subjectId = await CreateSubject(client, "Yazilim");

        var level = await client.PostAsJsonAsync(
            new Uri($"/api/v1/subjects/{subjectId}/levels", UriKind.Relative),
            new { code = "beginner", name = "Baslangic", sortOrder = 1 },
            TestContext.Current.CancellationToken);

        level.StatusCode.ShouldBe(HttpStatusCode.OK);

        var levelId = (await level.Content.ReadFromJsonAsync<CreateLevelResult>(
            TestContext.Current.CancellationToken))!.LevelId;

        var course = await client.PostAsJsonAsync(
            new Uri("/api/v1/courses", UriKind.Relative),
            new
            {
                subjectId,
                levelId,
                title = "Baslangic Seviye Python",
                courseType = "Structured",
                defaultDurationMinutes = 60,
                minParticipants = 3,
                maxParticipants = 12
            },
            TestContext.Current.CancellationToken);

        course.StatusCode.ShouldBe(HttpStatusCode.OK);

        var created = await course.Content.ReadFromJsonAsync<CreateCourseResult>(
            TestJson.Options, TestContext.Current.CancellationToken);

        // Egitim taslak olarak baslar.
        created!.Status.ShouldBe(Domain.Catalog.CourseStatus.Draft);

        var moduleId = await AddModule(client, created.CourseId, "Temeller", 1);
        await AddLesson(client, moduleId, "Degiskenler", 1, 45);

        var publish = await client.PostAsync(
            new Uri($"/api/v1/courses/{created.CourseId}/publish", UriKind.Relative),
            content: null,
            TestContext.Current.CancellationToken);

        publish.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var detail = await client.GetFromJsonAsync<CourseDetail>(
            new Uri($"/api/v1/courses/{created.CourseId}", UriKind.Relative),
            TestJson.Options,
            TestContext.Current.CancellationToken);

        detail.ShouldNotBeNull();
        detail.Status.ShouldBe(Domain.Catalog.CourseStatus.Published);
        detail.LevelCode.ShouldBe("beginner");

        var module = detail.Modules.ShouldHaveSingleItem();
        module.Title.ShouldBe("Temeller");
        module.Lessons.ShouldHaveSingleItem().Title.ShouldBe("Degiskenler");
    }

    /// <summary>
    /// Yayinlanmamis egitim, katalogu yonetme izni olmayana gorunmemeli.
    /// </summary>
    [Fact]
    public async Task DraftCourse_IsHiddenFromReaders()
    {
        var (ownerClient, organizationId) = await NewOrganizationClient();

        var subjectId = await CreateSubject(ownerClient, "Matematik");
        var courseId = await CreateCourse(ownerClient, subjectId, "Taslak Egitim");

        // Sahip taslagi gorur.
        var ownerList = await ownerClient.GetFromJsonAsync<PagedResult<CourseListItem>>(
            new Uri("/api/v1/courses", UriKind.Relative),
            TestJson.Options,
            TestContext.Current.CancellationToken);

        ownerList!.Items.Select(i => i.Id).ShouldContain(courseId);

        // Yalnizca okuma izni olan uye goremez.
        var readerClient = await InviteReader(ownerClient, organizationId);

        var readerList = await readerClient.GetFromJsonAsync<PagedResult<CourseListItem>>(
            new Uri("/api/v1/courses", UriKind.Relative),
            TestJson.Options,
            TestContext.Current.CancellationToken);

        readerList!.Items.Select(i => i.Id).ShouldNotContain(courseId);

        // Dogrudan detay istegi de "bulunamadi" donmeli: "var ama goremezsin"
        // demek katalogun varligini ele verirdi.
        var detail = await readerClient.GetAsync(
            new Uri($"/api/v1/courses/{courseId}", UriKind.Relative),
            TestContext.Current.CancellationToken);

        detail.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        readerClient.Dispose();
    }

    /// <summary>
    /// Bir kurumun katalogu baska kurumdan gorunmemeli.
    /// </summary>
    [Fact]
    public async Task Catalog_IsIsolatedBetweenOrganizations()
    {
        var (firstClient, _) = await NewOrganizationClient();
        var firstSubjectId = await CreateSubject(firstClient, "Ingilizce");
        var firstCourseId = await CreateCourse(firstClient, firstSubjectId, "Konusma Kulubu", publish: true);

        var (secondClient, _) = await NewOrganizationClient();

        var subjects = await secondClient.GetFromJsonAsync<IReadOnlyList<SubjectListItem>>(
            new Uri("/api/v1/subjects", UriKind.Relative),
            TestJson.Options,
            TestContext.Current.CancellationToken);

        subjects!.Select(s => s.Id).ShouldNotContain(firstSubjectId);

        var courses = await secondClient.GetFromJsonAsync<PagedResult<CourseListItem>>(
            new Uri("/api/v1/courses", UriKind.Relative),
            TestJson.Options,
            TestContext.Current.CancellationToken);

        courses!.Items.Select(c => c.Id).ShouldNotContain(firstCourseId);

        // Kimlik bilinse bile erisilemez.
        var detail = await secondClient.GetAsync(
            new Uri($"/api/v1/courses/{firstCourseId}", UriKind.Relative),
            TestContext.Current.CancellationToken);

        detail.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        firstClient.Dispose();
        secondClient.Dispose();
    }

    /// <summary>
    /// Baska bir alanin seviyesi egitime atanamaz.
    /// </summary>
    [Fact]
    public async Task Course_WithLevelFromAnotherSubject_IsRejected()
    {
        var (client, _) = await NewOrganizationClient();

        var firstSubjectId = await CreateSubject(client, "Ingilizce");
        var secondSubjectId = await CreateSubject(client, "Matematik");

        var level = await client.PostAsJsonAsync(
            new Uri($"/api/v1/subjects/{firstSubjectId}/levels", UriKind.Relative),
            new { code = "A1", name = "A1", sortOrder = 1 },
            TestContext.Current.CancellationToken);

        var levelId = (await level.Content.ReadFromJsonAsync<CreateLevelResult>(
            TestContext.Current.CancellationToken))!.LevelId;

        var response = await client.PostAsJsonAsync(
            new Uri("/api/v1/courses", UriKind.Relative),
            new
            {
                subjectId = secondSubjectId,
                levelId,
                title = "Yanlis Seviye",
                courseType = "Structured",
                defaultDurationMinutes = 60,
                minParticipants = 1,
                maxParticipants = 10
            },
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(
            TestContext.Current.CancellationToken);

        problem.GetProperty("errorCode").GetString().ShouldBe("catalog.level_subject_mismatch");

        client.Dispose();
    }

    [Fact]
    public async Task PublishedCourse_CannotBePublishedAgain()
    {
        var (client, _) = await NewOrganizationClient();

        var subjectId = await CreateSubject(client, "Yazilim");
        var courseId = await CreateCourse(client, subjectId, "Tekrar Yayin", publish: true);

        var second = await client.PostAsync(
            new Uri($"/api/v1/courses/{courseId}/publish", UriKind.Relative),
            content: null,
            TestContext.Current.CancellationToken);

        second.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        client.Dispose();
    }

    [Fact]
    public async Task CourseList_IsPaged()
    {
        var (client, _) = await NewOrganizationClient();
        var subjectId = await CreateSubject(client, "Yazilim");

        for (var i = 0; i < 3; i++)
        {
            await CreateCourse(client, subjectId, $"Egitim {i}", publish: true);
        }

        var firstPage = await client.GetFromJsonAsync<PagedResult<CourseListItem>>(
            new Uri("/api/v1/courses?page=1&pageSize=2", UriKind.Relative),
            TestJson.Options,
            TestContext.Current.CancellationToken);

        firstPage!.Items.Count.ShouldBe(2);
        firstPage.TotalCount.ShouldBe(3);
        firstPage.PageSize.ShouldBe(2);

        var secondPage = await client.GetFromJsonAsync<PagedResult<CourseListItem>>(
            new Uri("/api/v1/courses?page=2&pageSize=2", UriKind.Relative),
            TestJson.Options,
            TestContext.Current.CancellationToken);

        secondPage!.Items.ShouldHaveSingleItem();

        // Sayfalar ayrismali: ayni kayit iki sayfada birden gorunmemeli.
        firstPage.Items.Select(i => i.Id)
            .Intersect(secondPage.Items.Select(i => i.Id))
            .ShouldBeEmpty();

        client.Dispose();
    }

    /// <summary>
    /// Yeni bir kurum kurup sahibi olarak oturum acmis istemci dondurur.
    /// </summary>
    private async Task<(HttpClient Client, Guid OrganizationId)> NewOrganizationClient()
    {
        var client = fixture.CreateClient();
        await SignIn(client, "ogrenci@hotmail.com", "ogrenci123");

        var response = await client.PostAsJsonAsync(
            new Uri("/api/v1/organizations", UriKind.Relative),
            new
            {
                name = "Katalog Testi",
                slug = $"katalog-{Guid.CreateVersion7():N}"[..24],
                organizationType = "Provider",
                timeZoneId = "Europe/Istanbul",
                defaultCurrency = "TRY"
            },
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var organization = await response.Content.ReadFromJsonAsync<CreateOrganizationResult>(
            TestContext.Current.CancellationToken);

        client.DefaultRequestHeaders.Add(OrganizationHeader, organization!.OrganizationId.ToString());

        return (client, organization.OrganizationId);
    }

    /// <summary>
    /// Kuruma yalnizca okuma izni olan bir uye davet eder ve onun istemcisini dondurur.
    /// </summary>
    private async Task<HttpClient> InviteReader(HttpClient ownerClient, Guid organizationId)
    {
        var studentRoleId = await SystemRoleId("student");

        var invited = await ownerClient.PostAsJsonAsync(
            new Uri("/api/v1/organizations/members", UriKind.Relative),
            new { email = "ogretmen@hotmail.com", roleId = studentRoleId },
            TestContext.Current.CancellationToken);

        invited.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Davet Invited durumunda baslar; kiraci cozumlemesinin gecmesi icin
        // uyelik aktiflestirilir. Kabul ucu henuz yok.
        await ActivateMembership(organizationId);

        var client = fixture.CreateClient();
        await SignIn(client, "ogretmen@hotmail.com", "ogretmen123");
        client.DefaultRequestHeaders.Add(OrganizationHeader, organizationId.ToString());

        return client;
    }

    private async Task ActivateMembership(Guid organizationId)
    {
        await using var context = fixture.CreateContext();

        var membership = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions
            .FirstAsync(
                context.Memberships.Where(m => m.OrganizationId == organizationId
                                               && m.Status == Domain.Identity.MembershipStatus.Invited),
                TestContext.Current.CancellationToken);

        membership.Accept(DateTimeOffset.UtcNow);

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task<Guid> SystemRoleId(string code)
    {
        await using var context = fixture.CreateContext();

        var role = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions
            .FirstAsync(
                context.Roles.Where(r => r.Code == code && r.OrganizationId == null),
                TestContext.Current.CancellationToken);

        return role.Id;
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

    private static async Task<Guid> CreateCourse(
        HttpClient client,
        Guid subjectId,
        string title,
        bool publish = false)
    {
        var response = await client.PostAsJsonAsync(
            new Uri("/api/v1/courses", UriKind.Relative),
            new
            {
                subjectId,
                title,
                courseType = "DropIn",
                defaultDurationMinutes = 45,
                minParticipants = 1,
                maxParticipants = 10
            },
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var created = await response.Content.ReadFromJsonAsync<CreateCourseResult>(
            TestJson.Options, TestContext.Current.CancellationToken);

        if (publish)
        {
            var published = await client.PostAsync(
                new Uri($"/api/v1/courses/{created!.CourseId}/publish", UriKind.Relative),
                content: null,
                TestContext.Current.CancellationToken);

            published.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        }

        return created!.CourseId;
    }

    private static async Task<Guid> AddModule(HttpClient client, Guid courseId, string title, int sortOrder)
    {
        var response = await client.PostAsJsonAsync(
            new Uri($"/api/v1/courses/{courseId}/modules", UriKind.Relative),
            new { title, sortOrder, description = (string?)null },
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(
            TestContext.Current.CancellationToken);

        return body.GetProperty("moduleId").GetGuid();
    }

    private static async Task AddLesson(
        HttpClient client,
        Guid moduleId,
        string title,
        int sortOrder,
        int estimatedDurationMinutes)
    {
        var response = await client.PostAsJsonAsync(
            new Uri($"/api/v1/courses/modules/{moduleId}/lessons", UriKind.Relative),
            new { title, sortOrder, estimatedDurationMinutes, description = (string?)null },
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
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
}
