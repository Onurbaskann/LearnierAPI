using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Globalization;
using Learnier.Application.Common.Models;
using Learnier.Application.Features.Authentication.Commands.LoginUser;
using Learnier.Application.Features.Catalog.Commands.CreateCourse;
using Learnier.Application.Features.Catalog.Commands.CreateSubject;
using Learnier.Application.Features.Organizations.Commands.CreateOrganization;
using Learnier.Application.Features.Scheduling.Commands.CreateClassGroup;
using Learnier.Application.Features.Scheduling.Commands.BookInstructorSlot;
using Learnier.Application.Features.Scheduling.Commands.CreateSession;
using Learnier.Application.Features.Scheduling.Queries;
using Learnier.Application.Features.Teaching.Commands.CreateInstructorProfile;
using Learnier.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Learnier.IntegrationTests;

/// <summary>
/// Oturum planlama: egitmen atama ve kiraci izolasyonu.
/// </summary>
public sealed class SchedulingEndpointTests(AuthApiFixture fixture) : IClassFixture<AuthApiFixture>
{
    private const string OrganizationHeader = "X-Organization-Id";

    [Fact]
    public async Task InstructorSlot_CanBeListedBookedCancelledAndReopened()
    {
        var context = await NewOrganization();
        var (courseId, subjectId) = await CreatePublishedPrivateCourse(context.Client);
        var profileId = await CreateInstructor(context);

        (await context.Client.PostAsync(
            new Uri($"/api/v1/instructors/{profileId}/activate", UriKind.Relative),
            content: null,
            TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await context.Client.PostAsJsonAsync(
            new Uri($"/api/v1/instructors/{profileId}/subjects", UriKind.Relative),
            new { subjectId, levelId = (Guid?)null },
            TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.OK);

        var localDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7));
        var localStart = new DateTimeOffset(
            localDate.ToDateTime(new TimeOnly(10, 0)),
            TimeSpan.FromHours(3));
        var rangeStart = new DateTimeOffset(
            localDate.ToDateTime(TimeOnly.MinValue),
            TimeSpan.FromHours(3));
        var rangeEnd = rangeStart.AddDays(1);

        (await context.Client.PostAsJsonAsync(
            new Uri($"/api/v1/instructors/{profileId}/availabilities", UriKind.Relative),
            new
            {
                dayOfWeek = localStart.DayOfWeek.ToString(),
                startLocalTime = "10:00:00",
                endLocalTime = "12:00:00",
                validFrom = localDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                validUntil = (string?)null
            },
            TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.OK);

        var slotsUri = new Uri(
            $"/api/v1/instructors/{profileId}/slots?courseId={courseId}"
            + $"&from={Uri.EscapeDataString(rangeStart.ToString("O"))}"
            + $"&until={Uri.EscapeDataString(rangeEnd.ToString("O"))}",
            UriKind.Relative);

        var initialSlots = await context.Client.GetFromJsonAsync<
            IReadOnlyList<InstructorSlotListItem>>(
            slotsUri,
            TestJson.Options,
            TestContext.Current.CancellationToken);

        initialSlots!.Count.ShouldBe(2);
        initialSlots.ShouldAllBe(slot => slot.IsAvailable);

        var booked = await context.Client.PostAsJsonAsync(
            new Uri($"/api/v1/instructors/{profileId}/bookings", UriKind.Relative),
            new { courseId, startsAt = initialSlots[0].StartsAt },
            TestContext.Current.CancellationToken);

        booked.StatusCode.ShouldBe(HttpStatusCode.OK);
        var reservation = await booked.Content.ReadFromJsonAsync<BookInstructorSlotResult>(
            TestJson.Options,
            TestContext.Current.CancellationToken);
        reservation!.Status.ShouldBe(Domain.Scheduling.BookingStatus.Reserved);

        var occupiedSlots = await context.Client.GetFromJsonAsync<
            IReadOnlyList<InstructorSlotListItem>>(
            slotsUri,
            TestJson.Options,
            TestContext.Current.CancellationToken);
        occupiedSlots!.Single(slot => slot.StartsAt == reservation.StartsAt)
            .IsAvailable.ShouldBeFalse();

        var bookings = await context.Client.GetFromJsonAsync<PagedResult<LearnerBookingListItem>>(
            new Uri("/api/v1/bookings/me", UriKind.Relative),
            TestJson.Options,
            TestContext.Current.CancellationToken);
        bookings!.Items.Single(item => item.Id == reservation.BookingId)
            .SessionId.ShouldBe(reservation.SessionId);

        var cancelled = await context.Client.DeleteAsync(
            new Uri($"/api/v1/bookings/{reservation.BookingId}", UriKind.Relative),
            TestContext.Current.CancellationToken);
        cancelled.StatusCode.ShouldBe(HttpStatusCode.OK);

        var reopenedSlots = await context.Client.GetFromJsonAsync<
            IReadOnlyList<InstructorSlotListItem>>(
            slotsUri,
            TestJson.Options,
            TestContext.Current.CancellationToken);
        reopenedSlots!.Single(slot => slot.StartsAt == reservation.StartsAt)
            .IsAvailable.ShouldBeTrue();

        context.Dispose();
    }

    [Fact]
    public async Task Session_TimesWithOffset_AreStoredAsUtc()
    {
        var context = await NewOrganization();
        var courseId = await CreateCourse(context.Client);
        var startsAt = new DateTimeOffset(2026, 9, 7, 10, 0, 0, TimeSpan.FromHours(3));

        var sessionId = await CreateSession(
            context.Client,
            courseId,
            startsAt,
            startsAt.AddHours(1));

        var detail = await context.Client.GetFromJsonAsync<SessionDetail>(
            new Uri($"/api/v1/sessions/{sessionId}", UriKind.Relative),
            TestJson.Options,
            TestContext.Current.CancellationToken);

        detail!.StartsAt.Offset.ShouldBe(TimeSpan.Zero);
        detail.StartsAt.ShouldBe(startsAt.ToUniversalTime());
        detail.EndsAt.Offset.ShouldBe(TimeSpan.Zero);

        context.Dispose();
    }

    [Fact]
    public async Task Learner_CanListOnlyOwnBookings()
    {
        var context = await NewOrganization();
        var courseId = await CreateCourse(context.Client);
        var profileId = await CreateInstructor(context);
        var startsAt = DateTimeOffset.UtcNow.AddDays(8);
        var sessionId = await CreateSession(
            context.Client, courseId, startsAt, startsAt.AddHours(1));

        (await AssignInstructor(context.Client, sessionId, profileId))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var booking = await context.Client.PostAsJsonAsync(
            new Uri($"/api/v1/sessions/{sessionId}/bookings", UriKind.Relative),
            new { learnerUserId = (Guid?)null },
            TestContext.Current.CancellationToken);

        booking.StatusCode.ShouldBe(HttpStatusCode.OK);

        var result = await context.Client.GetFromJsonAsync<PagedResult<LearnerBookingListItem>>(
            new Uri($"/api/v1/bookings/me?from={Uri.EscapeDataString(startsAt.AddDays(-1).ToString("O"))}", UriKind.Relative),
            TestJson.Options,
            TestContext.Current.CancellationToken);

        var item = result!.Items.ShouldHaveSingleItem();
        item.SessionId.ShouldBe(sessionId);
        item.CourseTitle.ShouldBe("Planlama Testi");
        item.Status.ShouldBe(Domain.Scheduling.BookingStatus.Reserved);
        item.Instructors.ShouldHaveSingleItem().InstructorProfileId.ShouldBe(profileId);

        var other = await NewOrganization();
        var otherResult = await other.Client.GetFromJsonAsync<PagedResult<LearnerBookingListItem>>(
            new Uri("/api/v1/bookings/me", UriKind.Relative),
            TestJson.Options,
            TestContext.Current.CancellationToken);

        otherResult!.Items.ShouldBeEmpty();

        context.Dispose();
        other.Dispose();
    }

    /// <summary>
    /// Ayni egitmen ayni saatte iki oturuma atanamaz.
    /// </summary>
    /// <remarks>
    /// Kaynak dokumanin 13. bolumu bunu normal unique index'in cozemedigi bir durum
    /// olarak isaret ediyor: cakisma esitlik degil aralik kesisimi sorusudur.
    /// </remarks>
    [Fact]
    public async Task Instructor_CannotBeAssignedToOverlappingSessions()
    {
        var context = await NewOrganization();
        var courseId = await CreateCourse(context.Client);
        var profileId = await CreateInstructor(context);

        var startsAt = DateTimeOffset.UtcNow.AddDays(10);

        var first = await CreateSession(context.Client, courseId, startsAt, startsAt.AddHours(2));
        var assigned = await AssignInstructor(context.Client, first, profileId);

        assigned.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // 1 saat sonra baslayan oturum ilkiyle kesisiyor.
        var overlapping = await CreateSession(
            context.Client, courseId, startsAt.AddHours(1), startsAt.AddHours(3));

        var conflict = await AssignInstructor(context.Client, overlapping, profileId);

        conflict.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        var problem = await conflict.Content.ReadFromJsonAsync<JsonElement>(
            TestContext.Current.CancellationToken);

        problem.GetProperty("errorCode").GetString().ShouldBe("scheduling.instructor_busy");

        context.Dispose();
    }

    /// <summary>
    /// Bitisi digerinin baslangicina esit olan oturumlar cakisma sayilmaz.
    /// </summary>
    [Fact]
    public async Task Instructor_CanBeAssignedToAdjacentSessions()
    {
        var context = await NewOrganization();
        var courseId = await CreateCourse(context.Client);
        var profileId = await CreateInstructor(context);

        var startsAt = DateTimeOffset.UtcNow.AddDays(11);

        var first = await CreateSession(context.Client, courseId, startsAt, startsAt.AddHours(2));
        (await AssignInstructor(context.Client, first, profileId))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var adjacent = await CreateSession(
            context.Client, courseId, startsAt.AddHours(2), startsAt.AddHours(4));

        (await AssignInstructor(context.Client, adjacent, profileId))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        context.Dispose();
    }

    /// <summary>
    /// Baska bir kurumun oturumuna rezervasyon yapilamaz.
    /// </summary>
    [Fact]
    public async Task Session_IsIsolatedBetweenOrganizations()
    {
        var first = await NewOrganization();
        var courseId = await CreateCourse(first.Client);
        var startsAt = DateTimeOffset.UtcNow.AddDays(12);
        var sessionId = await CreateSession(first.Client, courseId, startsAt, startsAt.AddHours(1));

        var second = await NewOrganization();

        // Kimlik bilinse bile baska kurumdan erisilemez.
        var booking = await second.Client.PostAsJsonAsync(
            new Uri($"/api/v1/sessions/{sessionId}/bookings", UriKind.Relative),
            new { learnerUserId = (Guid?)null },
            TestContext.Current.CancellationToken);

        booking.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        first.Dispose();
        second.Dispose();
    }

    /// <summary>
    /// Baslamis oturuma rezervasyon yapilamaz.
    /// </summary>
    [Fact]
    public async Task PastSession_IsNotBookable()
    {
        var context = await NewOrganization();
        var courseId = await CreateCourse(context.Client);

        // Gecmis tarihli oturum: dogrudan veritabanina yazilir, cunku uc
        // gelecekteki tarihleri bekliyor.
        var startsAt = DateTimeOffset.UtcNow.AddDays(5);
        var sessionId = await CreateSession(context.Client, courseId, startsAt, startsAt.AddHours(1));

        await using (var db = fixture.CreateContext())
        {
            var session = await db.LessonSessions
                .IgnoreQueryFilters()
                .FirstAsync(s => s.Id == sessionId, TestContext.Current.CancellationToken);

            db.Entry(session).Property(s => s.StartsAt)
                .CurrentValue = DateTimeOffset.UtcNow.AddHours(-2);
            db.Entry(session).Property(s => s.EndsAt)
                .CurrentValue = DateTimeOffset.UtcNow.AddHours(-1);

            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var booking = await context.Client.PostAsJsonAsync(
            new Uri($"/api/v1/sessions/{sessionId}/bookings", UriKind.Relative),
            new { learnerUserId = (Guid?)null },
            TestContext.Current.CancellationToken);

        booking.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        context.Dispose();
    }

    [Fact]
    public async Task Scheduling_CanBeReadAndRemainsTenantIsolated()
    {
        var context = await NewOrganization();
        var courseId = await CreateCourse(context.Client);
        var profileId = await CreateInstructor(context);
        var startsAt = DateTimeOffset.UtcNow.AddDays(20);
        var sessionId = await CreateSession(
            context.Client, courseId, startsAt, startsAt.AddHours(1));

        (await AssignInstructor(context.Client, sessionId, profileId))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var classGroupResponse = await context.Client.PostAsJsonAsync(
            new Uri("/api/v1/class-groups", UriKind.Relative),
            new
            {
                courseId,
                name = "Hafta Ici Grubu",
                deliveryType = "Cohort",
                capacity = 12,
                startsOn = "2026-09-01",
                endsOn = "2026-12-31"
            },
            TestContext.Current.CancellationToken);

        classGroupResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var classGroup = await classGroupResponse.Content.ReadFromJsonAsync<CreateClassGroupResult>(
            TestJson.Options, TestContext.Current.CancellationToken);
        classGroup.ShouldNotBeNull();

        var sessions = await context.Client.GetFromJsonAsync<PagedResult<SessionListItem>>(
            new Uri($"/api/v1/sessions?courseId={courseId}", UriKind.Relative),
            TestJson.Options,
            TestContext.Current.CancellationToken);

        sessions!.Items.Single(s => s.Id == sessionId).CourseId.ShouldBe(courseId);

        var sessionDetail = await context.Client.GetFromJsonAsync<SessionDetail>(
            new Uri($"/api/v1/sessions/{sessionId}", UriKind.Relative),
            TestJson.Options,
            TestContext.Current.CancellationToken);

        sessionDetail!.Instructors.ShouldHaveSingleItem()
            .InstructorProfileId.ShouldBe(profileId);

        var booking = await context.Client.PostAsJsonAsync(
            new Uri($"/api/v1/sessions/{sessionId}/bookings", UriKind.Relative),
            new { learnerUserId = (Guid?)null },
            TestContext.Current.CancellationToken);

        booking.StatusCode.ShouldBe(HttpStatusCode.OK);

        var cancelled = await context.Client.PostAsJsonAsync(
            new Uri($"/api/v1/sessions/{sessionId}/cancel", UriKind.Relative),
            new { reason = "Program degisikligi" },
            TestContext.Current.CancellationToken);

        cancelled.StatusCode.ShouldBe(HttpStatusCode.OK);

        var cancelResult = await cancelled.Content.ReadFromJsonAsync<JsonElement>(
            TestContext.Current.CancellationToken);
        cancelResult.GetProperty("cancelledBookingCount").GetInt32().ShouldBe(1);

        var cancelledDetail = await context.Client.GetFromJsonAsync<SessionDetail>(
            new Uri($"/api/v1/sessions/{sessionId}", UriKind.Relative),
            TestJson.Options,
            TestContext.Current.CancellationToken);

        cancelledDetail!.Status.ShouldBe(Domain.Scheduling.LessonSessionStatus.Cancelled);
        cancelledDetail.ReservedSeatCount.ShouldBe(0);
        cancelledDetail.CancellationReason.ShouldBe("Program degisikligi");

        var classGroups = await context.Client.GetFromJsonAsync<PagedResult<ClassGroupListItem>>(
            new Uri($"/api/v1/class-groups?courseId={courseId}", UriKind.Relative),
            TestJson.Options,
            TestContext.Current.CancellationToken);

        classGroups!.Items.Single(g => g.Id == classGroup.ClassGroupId)
            .Name.ShouldBe("Hafta Ici Grubu");

        var classGroupDetail = await context.Client.GetFromJsonAsync<ClassGroupDetail>(
            new Uri($"/api/v1/class-groups/{classGroup.ClassGroupId}", UriKind.Relative),
            TestJson.Options,
            TestContext.Current.CancellationToken);

        classGroupDetail!.Capacity.ShouldBe(12);

        var other = await NewOrganization();

        (await other.Client.GetAsync(
            new Uri($"/api/v1/sessions/{sessionId}", UriKind.Relative),
            TestContext.Current.CancellationToken))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);

        (await other.Client.GetAsync(
            new Uri($"/api/v1/class-groups/{classGroup.ClassGroupId}", UriKind.Relative),
            TestContext.Current.CancellationToken))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);

        context.Dispose();
        other.Dispose();
    }

    private static Task<HttpResponseMessage> AssignInstructor(
        HttpClient client,
        Guid sessionId,
        Guid profileId)
        => client.PostAsJsonAsync(
            new Uri($"/api/v1/sessions/{sessionId}/instructors", UriKind.Relative),
            new { instructorProfileId = profileId, role = "Lead" },
            TestContext.Current.CancellationToken);

    private static async Task<Guid> CreateSession(
        HttpClient client,
        Guid courseId,
        DateTimeOffset startsAt,
        DateTimeOffset endsAt)
    {
        var response = await client.PostAsJsonAsync(
            new Uri("/api/v1/sessions", UriKind.Relative),
            new
            {
                courseId,
                sessionType = "Group",
                startsAt,
                endsAt,
                capacity = 10,
                minimumParticipants = 1
            },
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var created = await response.Content.ReadFromJsonAsync<CreateSessionResult>(
            TestJson.Options, TestContext.Current.CancellationToken);

        return created!.SessionId;
    }

    private static async Task<Guid> CreateCourse(HttpClient client)
    {
        var subject = await client.PostAsJsonAsync(
            new Uri("/api/v1/subjects", UriKind.Relative),
            new CreateSubjectCommand("Yazilim", $"alan-{Guid.CreateVersion7():N}"[..20], null),
            TestContext.Current.CancellationToken);

        var subjectId = (await subject.Content.ReadFromJsonAsync<CreateSubjectResult>(
            TestContext.Current.CancellationToken))!.SubjectId;

        var course = await client.PostAsJsonAsync(
            new Uri("/api/v1/courses", UriKind.Relative),
            new
            {
                subjectId,
                title = "Planlama Testi",
                courseType = "DropIn",
                defaultDurationMinutes = 60,
                minParticipants = 1,
                maxParticipants = 20
            },
            TestContext.Current.CancellationToken);

        var created = await course.Content.ReadFromJsonAsync<CreateCourseResult>(
            TestJson.Options, TestContext.Current.CancellationToken);

        return created!.CourseId;
    }

    private static async Task<(Guid CourseId, Guid SubjectId)> CreatePublishedPrivateCourse(
        HttpClient client)
    {
        var subject = await client.PostAsJsonAsync(
            new Uri("/api/v1/subjects", UriKind.Relative),
            new CreateSubjectCommand("Ingilizce", $"alan-{Guid.CreateVersion7():N}"[..20], null),
            TestContext.Current.CancellationToken);
        var subjectId = (await subject.Content.ReadFromJsonAsync<CreateSubjectResult>(
            TestContext.Current.CancellationToken))!.SubjectId;

        var course = await client.PostAsJsonAsync(
            new Uri("/api/v1/courses", UriKind.Relative),
            new
            {
                subjectId,
                title = "Birebir Ingilizce",
                courseType = "Private",
                defaultDurationMinutes = 60,
                minParticipants = 1,
                maxParticipants = 1
            },
            TestContext.Current.CancellationToken);
        var created = await course.Content.ReadFromJsonAsync<CreateCourseResult>(
            TestJson.Options,
            TestContext.Current.CancellationToken);

        (await client.PostAsync(
            new Uri($"/api/v1/courses/{created!.CourseId}/publish", UriKind.Relative),
            content: null,
            TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        return (created.CourseId, subjectId);
    }

    private async Task<Guid> CreateInstructor(OrganizationContext context)
    {
        var instructorRoleId = await SystemRoleId("instructor");

        var invited = await context.Client.PostAsJsonAsync(
            new Uri("/api/v1/organizations/members", UriKind.Relative),
            new { email = "ogretmen@hotmail.com", roleId = instructorRoleId },
            TestContext.Current.CancellationToken);

        invited.StatusCode.ShouldBe(HttpStatusCode.OK);

        var membershipId = await ActivateInvitedMembership(context.OrganizationId);

        var profile = await context.Client.PostAsJsonAsync(
            new Uri("/api/v1/instructors", UriKind.Relative),
            new CreateInstructorProfileCommand(membershipId, "Europe/Istanbul"),
            TestContext.Current.CancellationToken);

        profile.StatusCode.ShouldBe(HttpStatusCode.OK);

        var created = await profile.Content.ReadFromJsonAsync<CreateInstructorProfileResult>(
            TestJson.Options, TestContext.Current.CancellationToken);

        return created!.ProfileId;
    }

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

    private async Task<OrganizationContext> NewOrganization()
    {
        var client = fixture.CreateClient();
        await SignIn(client, "ogrenci@hotmail.com", "ogrenci123");

        var response = await client.PostAsJsonAsync(
            new Uri("/api/v1/organizations", UriKind.Relative),
            new
            {
                name = "Planlama Testi",
                slug = $"plan-{Guid.CreateVersion7():N}"[..20],
                organizationType = "Provider",
                timeZoneId = "Europe/Istanbul",
                defaultCurrency = "TRY"
            },
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var organization = await response.Content.ReadFromJsonAsync<CreateOrganizationResult>(
            TestContext.Current.CancellationToken);

        client.DefaultRequestHeaders.Add(
            OrganizationHeader, organization!.OrganizationId.ToString());

        return new OrganizationContext(client, organization.OrganizationId);
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

    private sealed record OrganizationContext(HttpClient Client, Guid OrganizationId)
    {
        public void Dispose() => Client.Dispose();
    }
}
