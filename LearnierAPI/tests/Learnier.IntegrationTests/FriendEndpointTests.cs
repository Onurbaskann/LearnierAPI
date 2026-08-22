using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Learnier.Application.Features.Authentication.Commands.LoginUser;
using Learnier.Application.Features.Friends;
using Shouldly;

namespace Learnier.IntegrationTests;

public sealed class FriendEndpointTests(AuthApiFixture fixture) : IClassFixture<AuthApiFixture>
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    [Fact]
    public async Task FriendRequest_CanBeSentAcceptedAndListedForBothUsers()
    {
        using var studentClient = fixture.CreateClient();
        using var peerClient = fixture.CreateClient();
        await AuthenticateAsync(studentClient, "ogrenci@hotmail.com", "ogrenci123");
        await AuthenticateAsync(peerClient, "paketsiz@hotmail.com", "paketsiz123");

        var invalidSearchResponse = await studentClient.GetAsync(
            "/api/v1/friends/search?query=o",
            TestContext.Current.CancellationToken);
        invalidSearchResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var initialSearchResponse = await studentClient.GetAsync(
            "/api/v1/friends/search?query=paketsiz",
            TestContext.Current.CancellationToken);
        initialSearchResponse.StatusCode.ShouldBe(
            HttpStatusCode.OK,
            await initialSearchResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var initialSearch = await initialSearchResponse.Content
            .ReadFromJsonAsync<IReadOnlyList<FriendUserSearchItem>>(
                JsonOptions,
                TestContext.Current.CancellationToken);
        var initialPeerResult = initialSearch.ShouldNotBeNull().ShouldHaveSingleItem();
        initialPeerResult.Email.ShouldBe("paketsiz@hotmail.com");
        initialPeerResult.RelationState.ShouldBe(FriendshipRelationState.None);

        var sendResponse = await studentClient.PostAsJsonAsync(
            "/api/v1/friends/requests",
            new SendFriendRequestCommand(initialPeerResult.UserId),
            TestContext.Current.CancellationToken);
        sendResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var request = await sendResponse.Content.ReadFromJsonAsync<FriendRequestListItem>(
            TestContext.Current.CancellationToken);
        request.ShouldNotBeNull();
        request.Email.ShouldBe("paketsiz@hotmail.com");

        var sentSearch = await studentClient.GetFromJsonAsync<IReadOnlyList<FriendUserSearchItem>>(
            "/api/v1/friends/search?query=paketsiz",
            JsonOptions,
            TestContext.Current.CancellationToken);
        sentSearch.ShouldNotBeNull().ShouldHaveSingleItem().RelationState
            .ShouldBe(FriendshipRelationState.SentRequest);

        var incomingSearch = await peerClient.GetFromJsonAsync<IReadOnlyList<FriendUserSearchItem>>(
            "/api/v1/friends/search?query=ogrenci",
            JsonOptions,
            TestContext.Current.CancellationToken);
        var incomingStudentResult = incomingSearch.ShouldNotBeNull().ShouldHaveSingleItem();
        incomingStudentResult.RelationState.ShouldBe(FriendshipRelationState.IncomingRequest);

        var sentResponse = await studentClient.GetAsync(
            "/api/v1/friends/requests/sent",
            TestContext.Current.CancellationToken);
        sentResponse.StatusCode.ShouldBe(
            HttpStatusCode.OK,
            await sentResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var sent = await sentResponse.Content.ReadFromJsonAsync<IReadOnlyList<FriendRequestListItem>>(
            TestContext.Current.CancellationToken);
        sent.ShouldNotBeNull().ShouldContain(item => item.RequestId == request.RequestId);

        var incoming = await peerClient.GetFromJsonAsync<IReadOnlyList<FriendRequestListItem>>(
            "/api/v1/friends/requests/incoming",
            TestContext.Current.CancellationToken);
        incoming.ShouldNotBeNull().ShouldContain(item => item.RequestId == request.RequestId);

        var senderAcceptResponse = await studentClient.PostAsync(
            $"/api/v1/friends/requests/{request.RequestId}/accept",
            null,
            TestContext.Current.CancellationToken);
        senderAcceptResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var acceptResponse = await peerClient.PostAsync(
            $"/api/v1/friends/requests/{request.RequestId}/accept",
            null,
            TestContext.Current.CancellationToken);
        acceptResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var studentFriends = await studentClient.GetFromJsonAsync<IReadOnlyList<FriendListItem>>(
            "/api/v1/friends",
            TestContext.Current.CancellationToken);
        studentFriends.ShouldNotBeNull().ShouldContain(friend => friend.Email == "paketsiz@hotmail.com");

        var peerFriends = await peerClient.GetFromJsonAsync<IReadOnlyList<FriendListItem>>(
            "/api/v1/friends",
            TestContext.Current.CancellationToken);
        peerFriends.ShouldNotBeNull().ShouldContain(friend => friend.Email == "ogrenci@hotmail.com");

        var duplicateResponse = await studentClient.PostAsJsonAsync(
            "/api/v1/friends/requests",
            new SendFriendRequestCommand(initialPeerResult.UserId),
            TestContext.Current.CancellationToken);
        duplicateResponse.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        var friendSearch = await studentClient.GetFromJsonAsync<IReadOnlyList<FriendUserSearchItem>>(
            "/api/v1/friends/search?query=paketsiz",
            JsonOptions,
            TestContext.Current.CancellationToken);
        var friendSearchResult = friendSearch.ShouldNotBeNull().ShouldHaveSingleItem();
        friendSearchResult.FriendshipId.ShouldBe(request.RequestId);
        friendSearchResult.RelationState.ShouldBe(FriendshipRelationState.Friends);

        var removeResponse = await studentClient.DeleteAsync(
            $"/api/v1/friends/{request.RequestId}",
            TestContext.Current.CancellationToken);
        removeResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var friendsAfterRemoval = await studentClient.GetFromJsonAsync<IReadOnlyList<FriendListItem>>(
            "/api/v1/friends",
            TestContext.Current.CancellationToken);
        friendsAfterRemoval.ShouldBeEmpty();

        var reverseSendResponse = await peerClient.PostAsJsonAsync(
            "/api/v1/friends/requests",
            new SendFriendRequestCommand(incomingStudentResult.UserId),
            TestContext.Current.CancellationToken);
        reverseSendResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var reverseRequest = await reverseSendResponse.Content.ReadFromJsonAsync<FriendRequestListItem>(
            TestContext.Current.CancellationToken);
        reverseRequest.ShouldNotBeNull();

        var recipientCancelResponse = await studentClient.DeleteAsync(
            $"/api/v1/friends/requests/{reverseRequest.RequestId}",
            TestContext.Current.CancellationToken);
        recipientCancelResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var cancelResponse = await peerClient.DeleteAsync(
            $"/api/v1/friends/requests/{reverseRequest.RequestId}",
            TestContext.Current.CancellationToken);
        cancelResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var incomingAfterCancellation = await studentClient.GetFromJsonAsync<IReadOnlyList<FriendRequestListItem>>(
            "/api/v1/friends/requests/incoming",
            TestContext.Current.CancellationToken);
        incomingAfterCancellation.ShouldBeEmpty();
    }

    [Fact]
    public async Task FriendRequest_IsRejectedWhenTargetIsNotAStudent()
    {
        using var studentClient = fixture.CreateClient();
        using var instructorClient = fixture.CreateClient();
        await AuthenticateAsync(studentClient, "ogrenci@hotmail.com", "ogrenci123");
        var instructor = await AuthenticateAsync(
            instructorClient, "ogretmen@hotmail.com", "ogretmen123");

        // Egitmen aramada hic gorunmez.
        var search = await studentClient.GetFromJsonAsync<IReadOnlyList<FriendUserSearchItem>>(
            "/api/v1/friends/search?query=ogretmen",
            JsonOptions,
            TestContext.Current.CancellationToken);
        search.ShouldNotBeNull().ShouldBeEmpty();

        // Kimlik dogrudan verilse bile sunucu reddeder.
        var response = await studentClient.PostAsJsonAsync(
            "/api/v1/friends/requests",
            new SendFriendRequestCommand(instructor.User.Id),
            TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken))
            .ShouldContain("friends.not_a_student");
    }

    [Fact]
    public async Task FriendRequest_IsRejectedWhenSenderIsNotAStudent()
    {
        using var studentClient = fixture.CreateClient();
        using var instructorClient = fixture.CreateClient();
        var student = await AuthenticateAsync(studentClient, "ogrenci@hotmail.com", "ogrenci123");
        await AuthenticateAsync(instructorClient, "ogretmen@hotmail.com", "ogretmen123");

        // Arkadaslik iki tarafli bir ogrenci iliskisi: egitmen hedefi ogrenci
        // olsa bile istek gonderemez.
        var response = await instructorClient.PostAsJsonAsync(
            "/api/v1/friends/requests",
            new SendFriendRequestCommand(student.User.Id),
            TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken))
            .ShouldContain("friends.sender_not_a_student");
    }

    private static async Task<LoginUserResult> AuthenticateAsync(
        HttpClient client,
        string email,
        string password)
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
        return login;
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
