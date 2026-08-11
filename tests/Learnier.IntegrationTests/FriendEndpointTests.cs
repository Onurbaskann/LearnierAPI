using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Learnier.Application.Features.Authentication.Commands.LoginUser;
using Learnier.Application.Features.Friends;
using Shouldly;

namespace Learnier.IntegrationTests;

public sealed class FriendEndpointTests(AuthApiFixture fixture) : IClassFixture<AuthApiFixture>
{
    [Fact]
    public async Task FriendRequest_CanBeSentAcceptedAndListedForBothUsers()
    {
        using var studentClient = fixture.CreateClient();
        using var instructorClient = fixture.CreateClient();
        await AuthenticateAsync(studentClient, "ogrenci@hotmail.com", "ogrenci123");
        await AuthenticateAsync(instructorClient, "ogretmen@hotmail.com", "ogretmen123");

        var sendResponse = await studentClient.PostAsJsonAsync(
            "/api/v1/friends/requests",
            new SendFriendRequestCommand("ogretmen@hotmail.com"),
            TestContext.Current.CancellationToken);
        sendResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var request = await sendResponse.Content.ReadFromJsonAsync<FriendRequestListItem>(
            TestContext.Current.CancellationToken);
        request.ShouldNotBeNull();
        request.Email.ShouldBe("ogretmen@hotmail.com");

        var sentResponse = await studentClient.GetAsync(
            "/api/v1/friends/requests/sent",
            TestContext.Current.CancellationToken);
        sentResponse.StatusCode.ShouldBe(
            HttpStatusCode.OK,
            await sentResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var sent = await sentResponse.Content.ReadFromJsonAsync<IReadOnlyList<FriendRequestListItem>>(
            TestContext.Current.CancellationToken);
        sent.ShouldNotBeNull().ShouldContain(item => item.RequestId == request.RequestId);

        var incoming = await instructorClient.GetFromJsonAsync<IReadOnlyList<FriendRequestListItem>>(
            "/api/v1/friends/requests/incoming",
            TestContext.Current.CancellationToken);
        incoming.ShouldNotBeNull().ShouldContain(item => item.RequestId == request.RequestId);

        var senderAcceptResponse = await studentClient.PostAsync(
            $"/api/v1/friends/requests/{request.RequestId}/accept",
            null,
            TestContext.Current.CancellationToken);
        senderAcceptResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var acceptResponse = await instructorClient.PostAsync(
            $"/api/v1/friends/requests/{request.RequestId}/accept",
            null,
            TestContext.Current.CancellationToken);
        acceptResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var studentFriends = await studentClient.GetFromJsonAsync<IReadOnlyList<FriendListItem>>(
            "/api/v1/friends",
            TestContext.Current.CancellationToken);
        studentFriends.ShouldNotBeNull().ShouldContain(friend => friend.Email == "ogretmen@hotmail.com");

        var instructorFriends = await instructorClient.GetFromJsonAsync<IReadOnlyList<FriendListItem>>(
            "/api/v1/friends",
            TestContext.Current.CancellationToken);
        instructorFriends.ShouldNotBeNull().ShouldContain(friend => friend.Email == "ogrenci@hotmail.com");

        var duplicateResponse = await studentClient.PostAsJsonAsync(
            "/api/v1/friends/requests",
            new SendFriendRequestCommand("ogretmen@hotmail.com"),
            TestContext.Current.CancellationToken);
        duplicateResponse.StatusCode.ShouldBe(HttpStatusCode.Conflict);
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
    }
}
