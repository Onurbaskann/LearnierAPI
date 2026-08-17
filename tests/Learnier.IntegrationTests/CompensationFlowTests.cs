using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Learnier.Application.Common.Abstractions;
using Learnier.Application.Features.Authentication.Commands.LoginUser;
using Learnier.Application.Features.Catalog.Commands.CreateCourse;
using Learnier.Application.Features.Catalog.Commands.CreateSubject;
using Learnier.Application.Features.Organizations.Commands.CreateOrganization;
using Learnier.Application.Features.Scheduling.Commands.CreateBooking;
using Learnier.Application.Features.Scheduling.Commands.OpenInstructorSlot;
using Learnier.Application.Features.Teaching.Commands.CreateInstructorProfile;
using Learnier.Domain.Billing;
using Learnier.Domain.Identity;
using Learnier.WebApi.Controllers;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Learnier.IntegrationTests;

/// <summary>
/// Urun ve teknik yol haritasindaki Faz 4 Senaryo A/B/C ile penalty kabul
/// kriterlerini gercek HTTP ve PostgreSQL akisi uzerinden dogrular.
/// </summary>
public sealed class CompensationFlowTests(AuthApiFixture fixture) : IClassFixture<AuthApiFixture>
{
    private const string OrganizationHeader = "X-Organization-Id";

    [Fact]
    public async Task ScenarioA_LateCancellation_AppliesSnapshottedPenaltyOnce_AndResets()
    {
        using var setup = await NewScenario();
        await ConfigurePenaltySteps(setup.OwnerClient, 10m, 15m);

        var cancelledLesson = await OpenAndBook(
            setup, DateTimeOffset.UtcNow.AddHours(1));
        await CancelByInstructor(setup, cancelledLesson.SessionId, "Gec iptal");

        var pending = await GetPenaltyHistory(setup);
        pending.CurrentLevel.ShouldBe(1);
        pending.PendingPercentage.ShouldBe(10m);
        pending.Events.ShouldHaveSingleItem().EventType.ShouldBe("LateCancellation");

        // Acik penalty eski orani snapshot olarak korumali.
        await ConfigurePenaltySteps(setup.OwnerClient, 30m, 40m);

        var completedLesson = await OpenAndBook(
            setup, DateTimeOffset.UtcNow.AddDays(3));
        await MoveSessionToPast(completedLesson.SessionId);
        await Complete(setup, completedLesson);

        // Tekrar complete ayni earning'i veya penalty uygulamasini uretmemeli.
        await Complete(setup, completedLesson);

        await using (var database = fixture.CreateContext())
        {
            var earning = await database.InstructorEarnings.SingleAsync(
                item => item.SessionId == completedLesson.SessionId,
                TestContext.Current.CancellationToken);
            earning.GrossAmount.ShouldBe(20m);
            earning.PenaltyPercentage.ShouldBe(10m);
            earning.PenaltyAmount.ShouldBe(2m);
            earning.NetAmount.ShouldBe(18m);

            (await database.InstructorEarnings.CountAsync(
                item => item.SessionId == completedLesson.SessionId,
                TestContext.Current.CancellationToken)).ShouldBe(1);
        }

        var applied = await GetPenaltyHistory(setup);
        applied.CurrentLevel.ShouldBe(0);
        applied.PendingPercentage.ShouldBe(0m);
        applied.Events.Count(item => item.EventType == "LateCancellation").ShouldBe(1);
        applied.Events.Count(item => item.EventType == "Applied").ShouldBe(1);

        // Sonradan tarife degisse de yazilmis earning degismemeli.
        await ConfigureRate(setup.OwnerClient, setup.SubjectId, 99m);
        await using var verification = fixture.CreateContext();
        (await verification.InstructorEarnings.SingleAsync(
            item => item.SessionId == completedLesson.SessionId,
            TestContext.Current.CancellationToken)).GrossAmount.ShouldBe(20m);
    }

    [Fact]
    public async Task ScenarioB_CancelledPenaltyLesson_AdvancesTier_ThenCompletionResets()
    {
        using var setup = await NewScenario();
        await ConfigurePenaltySteps(setup.OwnerClient, 10m, 15m, 25m);

        var first = await OpenAndBook(setup, DateTimeOffset.UtcNow.AddHours(1));
        await CancelByInstructor(setup, first.SessionId, "Ilk gec iptal");

        var penaltyLesson = await OpenAndBook(setup, DateTimeOffset.UtcNow.AddHours(2));
        await CancelByInstructor(setup, penaltyLesson.SessionId, "Penalty dersi de iptal");

        var tierTwo = await GetPenaltyHistory(setup);
        tierTwo.CurrentLevel.ShouldBe(2);
        tierTwo.PendingPercentage.ShouldBe(15m);
        tierTwo.Events.Count(item => item.EventType == "LateCancellation").ShouldBe(2);

        await using (var database = fixture.CreateContext())
        {
            (await database.InstructorEarnings.CountAsync(
                item => item.InstructorProfileId == setup.InstructorProfileId,
                TestContext.Current.CancellationToken)).ShouldBe(0);
        }

        var completed = await OpenAndBook(setup, DateTimeOffset.UtcNow.AddDays(3));
        await MoveSessionToPast(completed.SessionId);
        await Complete(setup, completed);

        await using (var database = fixture.CreateContext())
        {
            var earning = await database.InstructorEarnings.SingleAsync(
                item => item.SessionId == completed.SessionId,
                TestContext.Current.CancellationToken);
            earning.PenaltyPercentage.ShouldBe(15m);
            earning.NetAmount.ShouldBe(17m);
        }

        (await GetPenaltyHistory(setup)).CurrentLevel.ShouldBe(0);
    }

    [Fact]
    public async Task ScenarioC_TimelyCancellation_PreservesPendingPenaltyAndPolicySnapshot()
    {
        using var setup = await NewScenario();

        var late = await OpenAndBook(setup, DateTimeOffset.UtcNow.AddHours(1));
        await CancelByInstructor(setup, late.SessionId, "Gec iptal");

        // Bu ders varsayilan 4 saatlik policy ile acilir ve deadline snapshot'i alir.
        var timely = await OpenAndBook(setup, DateTimeOffset.UtcNow.AddHours(6));

        // Canli policy 8 saate ciksa bile daha once acilan dersin snapshot'i degismemeli.
        var policy = await setup.OwnerClient.PutAsJsonAsync(
            new Uri("/api/v1/admin/compensation/cancellation-policy", UriKind.Relative),
            new { studentRefundCutoffMinutes = 60, instructorPenaltyCutoffMinutes = 480 },
            TestContext.Current.CancellationToken);
        policy.StatusCode.ShouldBe(HttpStatusCode.OK);

        await CancelByInstructor(setup, timely.SessionId, "Zamaninda iptal");

        var preserved = await GetPenaltyHistory(setup);
        preserved.CurrentLevel.ShouldBe(1);
        preserved.PendingPercentage.ShouldBe(10m);
        preserved.Events.Count(item => item.EventType == "LateCancellation").ShouldBe(1);

        var completed = await OpenAndBook(setup, DateTimeOffset.UtcNow.AddDays(3));
        await MoveSessionToPast(completed.SessionId);
        await Complete(setup, completed);

        await using var database = fixture.CreateContext();
        var earning = await database.InstructorEarnings.SingleAsync(
            item => item.SessionId == completed.SessionId,
            TestContext.Current.CancellationToken);
        earning.PenaltyPercentage.ShouldBe(10m);
        (await GetPenaltyHistory(setup)).CurrentLevel.ShouldBe(0);
    }

    [Fact]
    public async Task PenaltyCap_Idempotency_AuditAndWaive_AreEnforced()
    {
        using var setup = await NewScenario();
        await ConfigurePenaltySteps(setup.OwnerClient, 10m, 15m);

        for (var index = 1; index <= 3; index++)
        {
            var lesson = await OpenAndBook(
                setup, DateTimeOffset.UtcNow.AddHours(index));
            await CancelByInstructor(setup, lesson.SessionId, $"Gec iptal {index}");

            if (index == 3)
            {
                // Ayni session icin tekrar gelen komut audit/state'i degistirmemeli.
                await CancelByInstructor(setup, lesson.SessionId, "Tekrar");
            }
        }

        var capped = await GetPenaltyHistory(setup);
        capped.CurrentLevel.ShouldBe(2);
        capped.PendingPercentage.ShouldBe(15m);
        var lateEvents = capped.Events
            .Where(item => item.EventType == "LateCancellation")
            .OrderBy(item => item.OccurredAt)
            .ToList();
        lateEvents.Count.ShouldBe(3);
        lateEvents.Select(item => item.Level).ShouldBe([1, 2, 2]);
        lateEvents.Select(item => item.Percentage).ShouldBe([10m, 15m, 15m]);

        var forbidden = await setup.InstructorClient.PostAsJsonAsync(
            new Uri(
                $"/api/v1/admin/compensation/instructors/{setup.InstructorProfileId}/penalties/waive",
                UriKind.Relative),
            new { reason = "Yetkisiz" },
            TestContext.Current.CancellationToken);
        forbidden.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var invalid = await setup.OwnerClient.PostAsJsonAsync(
            new Uri(
                $"/api/v1/admin/compensation/instructors/{setup.InstructorProfileId}/penalties/waive",
                UriKind.Relative),
            new { reason = "" },
            TestContext.Current.CancellationToken);
        invalid.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var waived = await setup.OwnerClient.PostAsJsonAsync(
            new Uri(
                $"/api/v1/admin/compensation/instructors/{setup.InstructorProfileId}/penalties/waive",
                UriKind.Relative),
            new { reason = "Destek kaydi ile duzeltildi" },
            TestContext.Current.CancellationToken);
        waived.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var history = await GetPenaltyHistory(setup);
        history.CurrentLevel.ShouldBe(0);
        var waiver = history.Events
            .Where(item => item.EventType == "Waived")
            .ShouldHaveSingleItem();
        waiver.Level.ShouldBe(2);
        waiver.Percentage.ShouldBe(15m);
        waiver.Reason.ShouldBe("Destek kaydi ile duzeltildi");
        waiver.ActorUserId.ShouldNotBeNull();
    }

    [Fact]
    public async Task ParallelCompletions_ApplyPendingPenaltyToOnlyOneEarning()
    {
        using var setup = await NewScenario();

        var late = await OpenAndBook(setup, DateTimeOffset.UtcNow.AddHours(1));
        await CancelByInstructor(setup, late.SessionId, "Gec iptal");

        var first = await OpenAndBook(setup, DateTimeOffset.UtcNow.AddDays(3));
        var second = await OpenAndBook(setup, DateTimeOffset.UtcNow.AddDays(4));
        await MoveSessionToPast(first.SessionId);
        await MoveSessionToPast(second.SessionId);

        var responses = await Task.WhenAll(
            CompleteResponse(setup, first),
            CompleteResponse(setup, second));

        foreach (var response in responses)
        {
            response.StatusCode.ShouldBe(
                HttpStatusCode.OK,
                await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        }

        await using var database = fixture.CreateContext();
        var earnings = await database.InstructorEarnings
            .Where(item => item.InstructorProfileId == setup.InstructorProfileId)
            .OrderBy(item => item.PenaltyPercentage)
            .ToListAsync(TestContext.Current.CancellationToken);
        earnings.Count.ShouldBe(2);
        earnings.Select(item => item.PenaltyPercentage).ShouldBe([0m, 10m]);

        var appliedCount = await database.InstructorPenaltyEvents.CountAsync(
            item => item.InstructorProfileId == setup.InstructorProfileId
                    && item.EventType == InstructorPenaltyEventType.Applied,
            TestContext.Current.CancellationToken);
        appliedCount.ShouldBe(1);
    }

    private async Task<ScenarioContext> NewScenario()
    {
        var owner = fixture.CreateClient();
        await SignIn(owner, "ogrenci@hotmail.com", "ogrenci123");

        var organizationResponse = await owner.PostAsJsonAsync(
            new Uri("/api/v1/organizations", UriKind.Relative),
            new
            {
                name = "Compensation Test",
                slug = $"comp-{Guid.CreateVersion7():N}"[..20],
                organizationType = "Provider",
                timeZoneId = "Europe/Istanbul",
                defaultCurrency = "TRY"
            },
            TestContext.Current.CancellationToken);
        organizationResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var organization = await organizationResponse.Content
            .ReadFromJsonAsync<CreateOrganizationResult>(
                TestJson.Options,
                TestContext.Current.CancellationToken);
        owner.DefaultRequestHeaders.Add(
            OrganizationHeader, organization!.OrganizationId.ToString());

        var subjectResponse = await owner.PostAsJsonAsync(
            new Uri("/api/v1/subjects", UriKind.Relative),
            new CreateSubjectCommand(
                "Ingilizce", $"comp-{Guid.CreateVersion7():N}"[..20], null),
            TestContext.Current.CancellationToken);
        subjectResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var subject = await subjectResponse.Content.ReadFromJsonAsync<CreateSubjectResult>(
            TestJson.Options,
            TestContext.Current.CancellationToken);

        var purchase = await owner.PostAsJsonAsync(
            new Uri("/api/v1/subscriptions/demo-purchases", UriKind.Relative),
            new
            {
                subjectId = subject!.SubjectId,
                lessonsPerWeek = 5,
                durationMonths = 6,
                lessonDurationMinutes = 50
            },
            TestContext.Current.CancellationToken);
        purchase.StatusCode.ShouldBe(HttpStatusCode.OK);

        var courseResponse = await owner.PostAsJsonAsync(
            new Uri("/api/v1/courses", UriKind.Relative),
            new
            {
                subjectId = subject.SubjectId,
                title = "Compensation Dersi",
                courseType = "Private",
                defaultDurationMinutes = 50,
                minParticipants = 1,
                maxParticipants = 1
            },
            TestContext.Current.CancellationToken);
        courseResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var course = await courseResponse.Content.ReadFromJsonAsync<CreateCourseResult>(
            TestJson.Options,
            TestContext.Current.CancellationToken);
        (await owner.PostAsync(
            new Uri($"/api/v1/courses/{course!.CourseId}/publish", UriKind.Relative),
            null,
            TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var instructorRoleId = await SystemRoleId("instructor");
        var invitation = await owner.PostAsJsonAsync(
            new Uri("/api/v1/organizations/members", UriKind.Relative),
            new { email = "ogretmen@hotmail.com", roleId = instructorRoleId },
            TestContext.Current.CancellationToken);
        invitation.StatusCode.ShouldBe(HttpStatusCode.OK);
        var membershipId = await ActivateInvitedMembership(organization.OrganizationId);

        var profileResponse = await owner.PostAsJsonAsync(
            new Uri("/api/v1/instructors", UriKind.Relative),
            new CreateInstructorProfileCommand(membershipId, "Europe/Istanbul"),
            TestContext.Current.CancellationToken);
        profileResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var profile = await profileResponse.Content
            .ReadFromJsonAsync<CreateInstructorProfileResult>(
                TestJson.Options,
                TestContext.Current.CancellationToken);

        (await owner.PostAsync(
            new Uri($"/api/v1/instructors/{profile!.ProfileId}/activate", UriKind.Relative),
            null,
            TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await owner.PostAsJsonAsync(
            new Uri($"/api/v1/instructors/{profile.ProfileId}/subjects", UriKind.Relative),
            new { subjectId = subject.SubjectId, levelId = (Guid?)null },
            TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.OK);

        await ConfigureRate(owner, subject.SubjectId, 20m);

        var instructor = fixture.CreateClient();
        await SignIn(instructor, "ogretmen@hotmail.com", "ogretmen123");
        instructor.DefaultRequestHeaders.Add(
            OrganizationHeader, organization.OrganizationId.ToString());

        return new ScenarioContext(
            owner,
            instructor,
            organization.OrganizationId,
            subject.SubjectId,
            course.CourseId,
            profile.ProfileId);
    }

    private static async Task<LessonContext> OpenAndBook(
        ScenarioContext setup,
        DateTimeOffset startsAt)
    {
        var openedResponse = await setup.InstructorClient.PostAsJsonAsync(
            new Uri("/api/v1/instructors/me/slots", UriKind.Relative),
            new
            {
                courseId = setup.CourseId,
                startsAt,
                lessonDurationMinutes = 50
            },
            TestContext.Current.CancellationToken);
        openedResponse.StatusCode.ShouldBe(
            HttpStatusCode.OK,
            await openedResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var opened = await openedResponse.Content.ReadFromJsonAsync<OpenInstructorSlotResult>(
            TestJson.Options,
            TestContext.Current.CancellationToken);

        var bookingResponse = await setup.OwnerClient.PostAsJsonAsync(
            new Uri($"/api/v1/sessions/{opened!.SessionId}/bookings", UriKind.Relative),
            new { learnerUserId = (Guid?)null, lessonDurationMinutes = 50 },
            TestContext.Current.CancellationToken);
        bookingResponse.StatusCode.ShouldBe(
            HttpStatusCode.OK,
            await bookingResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var booking = await bookingResponse.Content.ReadFromJsonAsync<CreateBookingResult>(
            TestJson.Options,
            TestContext.Current.CancellationToken);

        return new LessonContext(opened.SessionId, booking!.BookingId);
    }

    private static async Task CancelByInstructor(
        ScenarioContext setup,
        Guid sessionId,
        string reason)
    {
        var response = await setup.InstructorClient.PostAsJsonAsync(
            new Uri($"/api/v1/instructors/me/schedule/{sessionId}/cancel", UriKind.Relative),
            new { reason },
            TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(
            HttpStatusCode.OK,
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    private async Task MoveSessionToPast(Guid sessionId)
    {
        await using var database = fixture.CreateContext();
        var now = DateTimeOffset.UtcNow;
        await database.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE lesson_sessions SET starts_at = {now.AddMinutes(-60)}, ends_at = {now.AddMinutes(-10)} WHERE id = {sessionId}",
            TestContext.Current.CancellationToken);
    }

    private static Task<HttpResponseMessage> CompleteResponse(
        ScenarioContext setup,
        LessonContext lesson)
        => setup.InstructorClient.PostAsJsonAsync(
            new Uri($"/api/v1/sessions/{lesson.SessionId}/complete", UriKind.Relative),
            new
            {
                attendances = new[]
                {
                    new
                    {
                        bookingId = lesson.BookingId,
                        status = "Present",
                        attendedMinutes = 50
                    }
                }
            },
            TestContext.Current.CancellationToken);

    private static async Task Complete(ScenarioContext setup, LessonContext lesson)
    {
        var response = await CompleteResponse(setup, lesson);
        response.StatusCode.ShouldBe(
            HttpStatusCode.OK,
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    private static async Task<InstructorPenaltyHistory> GetPenaltyHistory(
        ScenarioContext setup)
    {
        var history = await setup.OwnerClient.GetFromJsonAsync<InstructorPenaltyHistory>(
            new Uri(
                $"/api/v1/admin/compensation/instructors/{setup.InstructorProfileId}/penalties",
                UriKind.Relative),
            TestJson.Options,
            TestContext.Current.CancellationToken);
        return history!;
    }

    private static async Task ConfigurePenaltySteps(
        HttpClient owner,
        params decimal[] percentages)
    {
        var response = await owner.PutAsJsonAsync(
            new Uri("/api/v1/admin/compensation/penalty-steps", UriKind.Relative),
            new { percentages },
            TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    private static async Task ConfigureRate(
        HttpClient owner,
        Guid subjectId,
        decimal amount)
    {
        var response = await owner.PutAsJsonAsync(
            new Uri("/api/v1/admin/compensation/rates", UriKind.Relative),
            new
            {
                subjectId,
                lessonDurationMinutes = 50,
                amount,
                currency = "USD"
            },
            TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private async Task<Guid> ActivateInvitedMembership(Guid organizationId)
    {
        await using var database = fixture.CreateContext();
        var membership = await database.Memberships.SingleAsync(
            item => item.OrganizationId == organizationId
                    && item.Status == MembershipStatus.Invited,
            TestContext.Current.CancellationToken);
        membership.Accept(DateTimeOffset.UtcNow);
        await database.SaveChangesAsync(TestContext.Current.CancellationToken);
        return membership.Id;
    }

    private async Task<Guid> SystemRoleId(string code)
    {
        await using var database = fixture.CreateContext();
        return await database.Roles
            .Where(item => item.Code == code && item.OrganizationId == null)
            .Select(item => item.Id)
            .SingleAsync(TestContext.Current.CancellationToken);
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
            new AuthenticationHeaderValue("Bearer", session!.AccessToken);
    }

    private sealed record LessonContext(Guid SessionId, Guid BookingId);

    private sealed record ScenarioContext(
        HttpClient OwnerClient,
        HttpClient InstructorClient,
        Guid OrganizationId,
        Guid SubjectId,
        Guid CourseId,
        Guid InstructorProfileId) : IDisposable
    {
        public void Dispose()
        {
            OwnerClient.Dispose();
            InstructorClient.Dispose();
        }
    }
}
