using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Learnier.Application.Common.Security;
using Learnier.Application.Features.Authentication.Commands.LoginUser;
using Learnier.Application.Features.Catalog.Commands.CreateSubject;
using Learnier.Application.Features.Clubs;
using Learnier.Application.Features.Clubs.Commands.CreateClub;
using Learnier.Domain.Billing;
using Learnier.Domain.Identity;
using Learnier.Domain.Social;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Learnier.IntegrationTests;

public sealed class ClubEndpointTests(AuthApiFixture fixture) : IClassFixture<AuthApiFixture>
{
    [Fact]
    public async Task Club_RequiresPackageAndPersistsMessages()
    {
        await PromoteInstructorAsync();

        using var adminClient = fixture.CreateClient();
        using var studentClient = fixture.CreateClient();
        await AuthenticateAsync(adminClient, "ogretmen@hotmail.com", "ogretmen123");
        await AuthenticateAsync(studentClient, "ogrenci@hotmail.com", "ogrenci123");

        var subjectResponse = await adminClient.PostAsJsonAsync(
            "/api/v1/subjects",
            new CreateSubjectCommand("İngilizce", $"ingilizce-{Guid.NewGuid():N}", null),
            TestContext.Current.CancellationToken);
        subjectResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var subject = await subjectResponse.Content.ReadFromJsonAsync<CreateSubjectResult>(
            TestContext.Current.CancellationToken);
        subject.ShouldNotBeNull();

        var createResponse = await adminClient.PostAsJsonAsync(
            "/api/v1/clubs",
            new CreateClubCommand(subject.SubjectId, "İngilizce Kulübü", "Paket kulübü"),
            TestContext.Current.CancellationToken);
        createResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var created = await createResponse.Content.ReadFromJsonAsync<CreateClubResult>(
            TestContext.Current.CancellationToken);
        created.ShouldNotBeNull();

        var duplicateResponse = await adminClient.PostAsJsonAsync(
            "/api/v1/clubs",
            new CreateClubCommand(subject.SubjectId, "İkinci kulüp", null),
            TestContext.Current.CancellationToken);
        duplicateResponse.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        var clubsWithoutPackage = await studentClient.GetFromJsonAsync<IReadOnlyList<ClubListItem>>(
            "/api/v1/clubs",
            TestJson.Options,
            TestContext.Current.CancellationToken);
        clubsWithoutPackage.ShouldBeEmpty();

        await GiveStudentSubjectAccessAsync(subject.SubjectId);

        var clubs = await studentClient.GetFromJsonAsync<IReadOnlyList<ClubListItem>>(
            "/api/v1/clubs",
            TestJson.Options,
            TestContext.Current.CancellationToken);
        var club = clubs.ShouldNotBeNull().ShouldHaveSingleItem();
        club.Id.ShouldBe(created.ClubId);
        var room = club.Rooms.ShouldHaveSingleItem();
        room.Type.ShouldBe(ClubRoomType.Text);

        var sendResponse = await studentClient.PostAsJsonAsync(
            $"/api/v1/clubs/rooms/{room.Id}/messages",
            new SendClubMessageCommand("Herkese merhaba!"),
            TestContext.Current.CancellationToken);
        sendResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var messages = await studentClient.GetFromJsonAsync<IReadOnlyList<ClubMessageItem>>(
            $"/api/v1/clubs/rooms/{room.Id}/messages",
            TestJson.Options,
            TestContext.Current.CancellationToken);
        messages.ShouldNotBeNull().ShouldHaveSingleItem().Body.ShouldBe("Herkese merhaba!");
    }

    private async Task PromoteInstructorAsync()
    {
        await using var context = fixture.CreateContext();
        var user = await context.Users.SingleAsync(
            user => user.Email == "ogretmen@hotmail.com",
            TestContext.Current.CancellationToken);
        var membership = await context.Memberships
            .Include(item => item.Roles)
            .SingleAsync(item => item.UserId == user.Id, TestContext.Current.CancellationToken);
        var adminRole = await context.Roles.SingleAsync(
            role => role.Code == SystemRoles.OrganizationAdmin,
            TestContext.Current.CancellationToken);

        if (membership.Roles.All(role => role.RoleId != adminRole.Id))
        {
            membership.AssignRole(adminRole.Id);
            context.Add(membership.Roles.Single(role => role.RoleId == adminRole.Id));
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
    }

    private async Task GiveStudentSubjectAccessAsync(Guid subjectId)
    {
        await using var context = fixture.CreateContext();
        var student = await context.Users.SingleAsync(
            user => user.Email == "ogrenci@hotmail.com",
            TestContext.Current.CancellationToken);
        var organizationId = await context.Memberships
            .Where(membership => membership.UserId == student.Id)
            .Select(membership => membership.OrganizationId)
            .SingleAsync(TestContext.Current.CancellationToken);
        var now = DateTimeOffset.UtcNow;

        var plan = SubscriptionPlan.Create(
            organizationId,
            $"İngilizce {Guid.NewGuid():N}",
            CatalogAccess.Restricted);
        plan.Activate();
        plan.CreatedAt = now;
        var price = plan.AddPrice("TRY", 100, BillingInterval.Month, 1, now);
        price.CreatedAt = now;

        var subscription = Subscription.CreateForUser(
            organizationId,
            student.Id,
            price.Id,
            now,
            now.AddMonths(1));
        subscription.Activate();
        subscription.CreatedAt = now;

        context.SubscriptionPlans.Add(plan);
        context.PlanSubjectAccess.Add(PlanSubjectAccess.Create(plan.Id, subjectId));
        context.Subscriptions.Add(subscription);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private static async Task AuthenticateAsync(HttpClient client, string email, string password)
    {
        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginUserCommand(email, password),
            TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var login = await response.Content.ReadFromJsonAsync<LoginUserResult>(
            TestContext.Current.CancellationToken);
        login.ShouldNotBeNull();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login.AccessToken);
        client.DefaultRequestHeaders.Add(
            "X-Organization-Id",
            login.Memberships.ShouldHaveSingleItem().OrganizationId.ToString());
    }
}
