using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Learnier.Application.Features.Authentication.Commands.LoginUser;
using Learnier.Application.Features.Friends;
using Learnier.Application.Features.Messages;
using Learnier.Domain.Catalog;
using Learnier.Domain.Scheduling;
using Learnier.Domain.Teaching;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Learnier.IntegrationTests;

/// <summary>
/// Birebir mesajlasma: yazisma kanalinin hangi kosullarda acildigi.
/// </summary>
/// <remarks>
/// Kanal iki kapidan biriyle acilir — kabul edilmis arkadaslik ya da ortak ders
/// gecmisi. Bu sinir yetkilendirmedir; asagidaki testler onu her iki yonde de
/// dogrular. Bkz. <c>MessagingAccess</c>.
/// </remarks>
public sealed class MessagingEndpointTests(AuthApiFixture fixture) : IClassFixture<AuthApiFixture>
{
    private const string StudentEmail = "ogrenci@hotmail.com";
    private const string StudentPassword = "ogrenci123";
    private const string PeerStudentEmail = "paketsiz@hotmail.com";
    private const string PeerStudentPassword = "paketsiz123";
    private const string InstructorEmail = "ogretmen@hotmail.com";
    private const string InstructorPassword = "ogretmen123";

    [Fact]
    public async Task Message_IsRejected_WhenUsersAreUnrelated()
    {
        await ResetMessagingState();
        using var studentClient = fixture.CreateClient();
        using var peerClient = fixture.CreateClient();
        await SignIn(studentClient, StudentEmail, StudentPassword);
        var peer = await SignIn(peerClient, PeerStudentEmail, PeerStudentPassword);

        var response = await SendMessage(studentClient, peer.User.Id, "selam");

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken))
            .ShouldContain("messages.not_reachable");
    }

    [Fact]
    public async Task Message_ToSelf_IsRejected()
    {
        await ResetMessagingState();
        using var studentClient = fixture.CreateClient();
        var student = await SignIn(studentClient, StudentEmail, StudentPassword);

        (await SendMessage(studentClient, student.User.Id, "kendime"))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Message_FlowsBetweenFriends_AndThreadClearsUnreadCount()
    {
        await ResetMessagingState();
        using var studentClient = fixture.CreateClient();
        using var peerClient = fixture.CreateClient();
        var student = await SignIn(studentClient, StudentEmail, StudentPassword);
        var peer = await SignIn(peerClient, PeerStudentEmail, PeerStudentPassword);
        await Befriend(studentClient, peerClient, peer.User.Id);

        (await SendMessage(studentClient, peer.User.Id, "Odevi yaptin mi?"))
            .StatusCode.ShouldBe(HttpStatusCode.OK);

        // Alici tarafta okunmamis olarak birikir.
        (await UnreadCount(peerClient)).ShouldBe(1);

        var conversations = await peerClient.GetFromJsonAsync<IReadOnlyList<ConversationListItem>>(
            "/api/v1/messages/conversations",
            TestJson.Options,
            TestContext.Current.CancellationToken);
        var conversation = conversations.ShouldNotBeNull().ShouldHaveSingleItem();
        conversation.PeerUserId.ShouldBe(student.User.Id);
        conversation.LastMessageBody.ShouldBe("Odevi yaptin mi?");
        conversation.LastMessageFromMe.ShouldBeFalse();
        conversation.UnreadCount.ShouldBe(1);

        // Yazismayi acmak ayni istekte okundu isaretler.
        var thread = await peerClient.GetFromJsonAsync<MessageThread>(
            $"/api/v1/messages/{student.User.Id}",
            TestJson.Options,
            TestContext.Current.CancellationToken);
        thread.ShouldNotBeNull().Messages.ShouldHaveSingleItem().IsMine.ShouldBeFalse();

        (await UnreadCount(peerClient)).ShouldBe(0);
    }

    [Fact]
    public async Task Message_IsRejected_AfterFriendshipIsRemoved()
    {
        await ResetMessagingState();
        using var studentClient = fixture.CreateClient();
        using var peerClient = fixture.CreateClient();
        await SignIn(studentClient, StudentEmail, StudentPassword);
        var peer = await SignIn(peerClient, PeerStudentEmail, PeerStudentPassword);
        var friendshipId = await Befriend(studentClient, peerClient, peer.User.Id);

        (await SendMessage(studentClient, peer.User.Id, "hala arkadasiz"))
            .StatusCode.ShouldBe(HttpStatusCode.OK);

        (await studentClient.DeleteAsync(
            $"/api/v1/friends/{friendshipId}",
            TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // Kanal arkadaslikla acilmisti; arkadaslik bitince kapanir.
        var response = await SendMessage(studentClient, peer.User.Id, "artik degiliz");
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken))
            .ShouldContain("messages.not_reachable");

        // Gecmis yazisma listeden de tamamen kalkar: acilamayan bir konusmanin
        // listede durmasi, tiklayinca 403 veren olu satir uretirdi.
        (await Conversations(studentClient)).ShouldBeEmpty();
        (await Conversations(peerClient)).ShouldBeEmpty();
    }

    [Fact]
    public async Task Message_FlowsBothWays_WhenALessonWasBooked()
    {
        await ResetMessagingState();
        using var studentClient = fixture.CreateClient();
        using var instructorClient = fixture.CreateClient();
        var student = await SignIn(studentClient, StudentEmail, StudentPassword);
        var instructor = await SignIn(instructorClient, InstructorEmail, InstructorPassword);

        // Arkadaslik yok; kanali yalnizca ders gecmisi acmali.
        await ArrangeBookedLesson(student.User.Id, instructor.User.Id);

        (await SendMessage(studentClient, instructor.User.Id, "Hocam bir sorum var."))
            .StatusCode.ShouldBe(HttpStatusCode.OK);
        (await SendMessage(instructorClient, student.User.Id, "Tabii, buyur."))
            .StatusCode.ShouldBe(HttpStatusCode.OK);

        var thread = await instructorClient.GetFromJsonAsync<MessageThread>(
            $"/api/v1/messages/{student.User.Id}",
            TestJson.Options,
            TestContext.Current.CancellationToken);
        thread.ShouldNotBeNull().Messages.Count.ShouldBe(2);

        // Ders gecmisi silinmedigi icin bu kanal arkadaslik kuralindan etkilenmez;
        // konusma her iki tarafin listesinde kalir.
        (await Conversations(studentClient)).ShouldHaveSingleItem()
            .PeerUserId.ShouldBe(instructor.User.Id);
        (await Conversations(instructorClient)).ShouldHaveSingleItem()
            .PeerUserId.ShouldBe(student.User.Id);
    }

    private static async Task<IReadOnlyList<ConversationListItem>> Conversations(HttpClient client)
    {
        var conversations = await client.GetFromJsonAsync<IReadOnlyList<ConversationListItem>>(
            "/api/v1/messages/conversations",
            TestJson.Options,
            TestContext.Current.CancellationToken);
        return conversations.ShouldNotBeNull();
    }

    /// <summary>
    /// Ogrenci ile egitmen arasinda tek bir rezervasyon olusturur.
    /// </summary>
    /// <remarks>
    /// Testin konusu yetkilendirme oldugu icin rezervasyon HTTP akisiyla degil
    /// dogrudan veritabanina kurulur: kredi, kontenjan ve rezervasyon penceresi
    /// kurallarinin burada dogrulanmasi gerekmiyor.
    /// </remarks>
    private async Task ArrangeBookedLesson(Guid learnerUserId, Guid instructorUserId)
    {
        await using var database = fixture.CreateContext();
        var cancellationToken = TestContext.Current.CancellationToken;

        var membership = await database.Memberships.SingleAsync(
            item => item.UserId == instructorUserId,
            cancellationToken);

        var profile = await database.InstructorProfiles.SingleOrDefaultAsync(
            item => item.MembershipId == membership.Id,
            cancellationToken);
        if (profile is null)
        {
            profile = InstructorProfile.Create(membership.Id, "Europe/Istanbul");
            database.InstructorProfiles.Add(profile);
        }

        var subject = await database.Subjects.FirstAsync(
            item => item.OrganizationId == membership.OrganizationId,
            cancellationToken);

        var course = Course.Create(
            membership.OrganizationId,
            subject.Id,
            "Mesajlasma testi dersi",
            CourseType.Private,
            defaultDurationMinutes: 50,
            minParticipants: 1,
            maxParticipants: 1);
        database.Courses.Add(course);

        var startsAt = DateTimeOffset.UtcNow.AddDays(-7);
        var session = LessonSession.Create(
            membership.OrganizationId,
            course.Id,
            SessionType.Private,
            startsAt,
            startsAt.AddMinutes(50),
            capacity: 1,
            minimumParticipants: 1);
        session.AssignInstructor(profile.Id, SessionInstructorRole.Lead);
        session.Book(
            learnerUserId,
            learnerUserId,
            BookingAccessSource.Subscription,
            startsAt.AddDays(-1),
            reservedSeatCount: 0);

        database.LessonSessions.Add(session);
        await database.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Mesaj ve arkadaslik kayitlarini temizler.
    /// </summary>
    /// <remarks>
    /// Sinifin testleri ayni veritabanini paylasir ve xUnit sirayi garanti etmez;
    /// sayac iddialarinin anlamli olmasi icin her test temiz sayfayla baslar.
    /// </remarks>
    private async Task ResetMessagingState()
    {
        await using var database = fixture.CreateContext();
        await database.Database.ExecuteSqlRawAsync(
            "DELETE FROM direct_messages; DELETE FROM friendships;",
            TestContext.Current.CancellationToken);
    }

    /// <returns>Kabul edilen arkadasligin kimligi.</returns>
    /// <remarks>
    /// Ayni sinifin testleri ayni veritabanini paylasir ve xUnit sirayi garanti
    /// etmez; bu yuzden yardimci, arkadaslik zaten kuruluysa da calisir.
    /// </remarks>
    private static async Task<Guid> Befriend(
        HttpClient requester,
        HttpClient recipient,
        Guid recipientUserId)
    {
        var requestResponse = await requester.PostAsJsonAsync(
            "/api/v1/friends/requests",
            new SendFriendRequestCommand(recipientUserId),
            TestContext.Current.CancellationToken);

        if (requestResponse.StatusCode is HttpStatusCode.Conflict)
        {
            var existing = await requester.GetFromJsonAsync<IReadOnlyList<FriendListItem>>(
                "/api/v1/friends",
                TestJson.Options,
                TestContext.Current.CancellationToken);
            return existing.ShouldNotBeNull()
                .Single(friend => friend.UserId == recipientUserId)
                .FriendshipId;
        }

        requestResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var request = await requestResponse.Content.ReadFromJsonAsync<FriendRequestListItem>(
            TestJson.Options,
            TestContext.Current.CancellationToken);

        (await recipient.PostAsync(
            $"/api/v1/friends/requests/{request!.RequestId}/accept",
            null,
            TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.OK);

        return request.RequestId;
    }

    private static Task<HttpResponseMessage> SendMessage(
        HttpClient client,
        Guid recipientUserId,
        string body)
        => client.PostAsJsonAsync(
            "/api/v1/messages",
            new SendMessageCommand(recipientUserId, body),
            TestContext.Current.CancellationToken);

    private static async Task<int> UnreadCount(HttpClient client)
    {
        var result = await client.GetFromJsonAsync<UnreadMessageCount>(
            "/api/v1/messages/unread-count",
            TestJson.Options,
            TestContext.Current.CancellationToken);
        return result.ShouldNotBeNull().Count;
    }

    private static async Task<LoginUserResult> SignIn(
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
            TestJson.Options,
            TestContext.Current.CancellationToken);
        login.ShouldNotBeNull();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login.AccessToken);
        return login;
    }
}
