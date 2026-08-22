using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Learnier.Application.Common.Abstractions;
using Learnier.Application.Common.Models;
using Learnier.Application.Features.Authentication.Commands.LoginUser;
using Learnier.Application.Features.Catalog.Commands.CreateCourse;
using Learnier.Application.Features.Catalog.Commands.CreateSubject;
using Learnier.Application.Features.Organizations.Commands.CreateOrganization;
using Learnier.Application.Features.Scheduling.Commands.CreateClassGroup;
using Learnier.Application.Features.Scheduling.Commands.OpenInstructorSlot;
using Learnier.Application.Features.Scheduling.Commands.CreateBooking;
using Learnier.Application.Features.Scheduling.Commands.CreateSession;
using Learnier.Application.Features.Scheduling.Queries;
using Learnier.Application.Features.Teaching.Commands.CreateInstructorProfile;
using Learnier.Application.Features.Teaching.Queries;
using Learnier.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Learnier.IntegrationTests;

/// <summary>
/// Oturum planlama: egitmen atama ve kiraci izolasyonu.
/// </summary>
public sealed class SchedulingEndpointTests(AuthApiFixture fixture) : IClassFixture<AuthApiFixture>
{
    private const string OrganizationHeader = "X-Organization-Id";

    [Fact]
    public async Task Instructor_CanCompleteOwnedEndedSession_AndCreditIsConsumedOnce()
    {
        var context = await NewOrganization();
        var (courseId, subjectId) = await CreatePublishedPrivateCourse(context.Client);

        var rateResponse = await context.Client.PutAsJsonAsync(
            new Uri("/api/v1/admin/compensation/rates", UriKind.Relative),
            new
            {
                subjectId,
                lessonDurationMinutes = 50,
                amount = 400m,
                currency = "TRY"
            },
            TestContext.Current.CancellationToken);
        rateResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var profileId = await CreateInstructor(context);

        (await context.Client.PostAsync(
            new Uri($"/api/v1/instructors/{profileId}/activate", UriKind.Relative),
            content: null,
            TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await context.Client.PostAsJsonAsync(
            new Uri($"/api/v1/instructors/{profileId}/subjects", UriKind.Relative),
            new { subjectId, levelId = (Guid?)null },
            TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.OK);

        using var instructorClient = fixture.CreateClient();
        await SignIn(instructorClient, "ogretmen@hotmail.com", "ogretmen123");
        instructorClient.DefaultRequestHeaders.Add(
            OrganizationHeader, context.OrganizationId.ToString());

        var openedResponse = await instructorClient.PostAsJsonAsync(
            new Uri("/api/v1/instructors/me/slots", UriKind.Relative),
            new
            {
                courseId,
                startsAt = DateTimeOffset.UtcNow.AddDays(2),
                lessonDurationMinutes = 50
            },
            TestContext.Current.CancellationToken);
        openedResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var opened = await openedResponse.Content.ReadFromJsonAsync<OpenInstructorSlotResult>(
            TestJson.Options,
            TestContext.Current.CancellationToken);

        var bookingResponse = await context.Client.PostAsJsonAsync(
            new Uri($"/api/v1/sessions/{opened!.SessionId}/bookings", UriKind.Relative),
            new { learnerUserId = (Guid?)null, lessonDurationMinutes = 50 },
            TestContext.Current.CancellationToken);
        bookingResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var booking = await bookingResponse.Content.ReadFromJsonAsync<CreateBookingResult>(
            TestJson.Options,
            TestContext.Current.CancellationToken);

        await using (var database = fixture.CreateContext())
        {
            var now = DateTimeOffset.UtcNow;
            await database.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE lesson_sessions SET starts_at = {now.AddMinutes(-60)}, ends_at = {now.AddMinutes(-10)} WHERE id = {opened.SessionId}",
                TestContext.Current.CancellationToken);

            var penalty = Domain.Billing.InstructorPenaltyState.Create(profileId);
            penalty.RegisterLateCancellation(opened.SessionId, 10m, now.AddHours(-1));
            database.InstructorPenaltyStates.Add(penalty);
            await database.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var completionBody = new
        {
            attendances = new[]
            {
                new
                {
                    bookingId = booking!.BookingId,
                    status = "Present",
                    attendedMinutes = 50
                }
            }
        };

        var completed = await instructorClient.PostAsJsonAsync(
            new Uri($"/api/v1/sessions/{opened.SessionId}/complete", UriKind.Relative),
            completionBody,
            TestContext.Current.CancellationToken);
        completed.StatusCode.ShouldBe(
            HttpStatusCode.OK,
            await completed.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        // Ayni istek yeniden gelirse ikinci attendance veya Consume yazilmamali.
        (await instructorClient.PostAsJsonAsync(
            new Uri($"/api/v1/sessions/{opened.SessionId}/complete", UriKind.Relative),
            completionBody,
            TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.OK);

        await using (var database = fixture.CreateContext())
        {
            (await database.LessonSessions.SingleAsync(
                item => item.Id == opened.SessionId,
                TestContext.Current.CancellationToken)).Status
                .ShouldBe(Domain.Scheduling.LessonSessionStatus.Completed);

            (await database.SessionBookings.SingleAsync(
                item => item.Id == booking.BookingId,
                TestContext.Current.CancellationToken)).Status
                .ShouldBe(Domain.Scheduling.BookingStatus.Attended);

            var attendance = await database.SessionAttendances.SingleAsync(
                item => item.BookingId == booking.BookingId,
                TestContext.Current.CancellationToken);
            attendance.Status.ShouldBe(Domain.Scheduling.AttendanceStatus.Present);
            attendance.AttendedMinutes.ShouldBe(50);

            (await database.CreditLedger.CountAsync(
                item => item.BookingId == booking.BookingId
                        && item.TransactionType == Domain.Billing.CreditTransactionType.Consume,
                TestContext.Current.CancellationToken)).ShouldBe(1);

            var earning = await database.InstructorEarnings.SingleAsync(
                item => item.SessionId == opened.SessionId
                        && item.InstructorProfileId == profileId,
                TestContext.Current.CancellationToken);
            earning.GrossAmount.ShouldBe(400m);
            earning.PenaltyPercentage.ShouldBe(10m);
            earning.PenaltyAmount.ShouldBe(40m);
            earning.NetAmount.ShouldBe(360m);

            (await database.InstructorPenaltyStates.SingleAsync(
                item => item.InstructorProfileId == profileId,
                TestContext.Current.CancellationToken)).Level.ShouldBe(0);
        }

        context.Dispose();
    }

    [Fact]
    public async Task InstructorSlot_IsEmptyUntilInstructorOpensOne_AndCanBeBookedOrClosed()
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

        var localStart = DateTimeOffset.UtcNow.AddDays(7);
        var rangeStart = new DateTimeOffset(
            localStart.Year, localStart.Month, localStart.Day, 0, 0, 0, TimeSpan.Zero);
        var rangeEnd = rangeStart.AddDays(2);

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

        initialSlots.ShouldBeEmpty();

        using var instructorClient = fixture.CreateClient();
        await SignIn(instructorClient, "ogretmen@hotmail.com", "ogretmen123");
        instructorClient.DefaultRequestHeaders.Add(
            OrganizationHeader, context.OrganizationId.ToString());

        var openedResponse = await instructorClient.PostAsJsonAsync(
            new Uri("/api/v1/instructors/me/slots", UriKind.Relative),
            new { courseId, startsAt = localStart, lessonDurationMinutes = 50 },
            TestContext.Current.CancellationToken);
        openedResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var opened = await openedResponse.Content.ReadFromJsonAsync<OpenInstructorSlotResult>(
            TestJson.Options,
            TestContext.Current.CancellationToken);

        var openedSlots = await context.Client.GetFromJsonAsync<
            IReadOnlyList<InstructorSlotListItem>>(
            slotsUri,
            TestJson.Options,
            TestContext.Current.CancellationToken);
        openedSlots.ShouldHaveSingleItem().SessionId.ShouldBe(opened!.SessionId);
        openedSlots.ShouldAllBe(slot => slot.IsAvailable);

        var booked = await context.Client.PostAsJsonAsync(
            new Uri($"/api/v1/sessions/{opened.SessionId}/bookings", UriKind.Relative),
            new { learnerUserId = (Guid?)null, lessonDurationMinutes = 50 },
            TestContext.Current.CancellationToken);
        booked.StatusCode.ShouldBe(
            HttpStatusCode.OK,
            await booked.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var reservation = await booked.Content.ReadFromJsonAsync<CreateBookingResult>(
            TestJson.Options,
            TestContext.Current.CancellationToken);
        reservation!.Status.ShouldBe(Domain.Scheduling.BookingStatus.Reserved);

        Guid meetingId;
        await using (var database = fixture.CreateContext())
        {
            var storedBooking = await database.SessionBookings.SingleAsync(
                item => item.Id == reservation.BookingId,
                TestContext.Current.CancellationToken);
            storedBooking.CreditLedgerEntryId.ShouldNotBeNull();

            var reserve = await database.CreditLedger.SingleAsync(
                item => item.BookingId == reservation.BookingId
                        && item.TransactionType == Domain.Billing.CreditTransactionType.Reserve,
                TestContext.Current.CancellationToken);
            reserve.Quantity.ShouldBe(-1);

            var pendingMeeting = await database.Meetings.SingleAsync(
                item => item.SessionId == opened.SessionId,
                TestContext.Current.CancellationToken);
            pendingMeeting.Status.ShouldBe(Domain.Scheduling.MeetingStatus.Pending);
            meetingId = pendingMeeting.Id;
        }

        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var processor = scope.ServiceProvider
                .GetRequiredService<IMeetingProvisioningProcessor>();
            (await processor.ProcessBatchAsync(TestContext.Current.CancellationToken))
                .ShouldBeGreaterThanOrEqualTo(1);
        }

        await using (var database = fixture.CreateContext())
        {
            var readyMeeting = await database.Meetings.SingleAsync(
                item => item.SessionId == opened.SessionId,
                TestContext.Current.CancellationToken);
            readyMeeting.Status.ShouldBe(Domain.Scheduling.MeetingStatus.Ready);
            readyMeeting.Provider.ShouldBe("sandbox");
            readyMeeting.ProvisioningAttemptCount.ShouldBe(1);
            readyMeeting.JoinUrl.ShouldNotBeNullOrWhiteSpace();
            readyMeeting.HostUrl.ShouldNotBeNullOrWhiteSpace();
            readyMeeting.JoinUrl.ShouldNotBe(readyMeeting.HostUrl);
        }

        // Rotalar artik vardir; dogru katilimci bile ders penceresinden once 409 alir.
        (await context.Client.GetAsync(
            new Uri($"/api/v1/meetings/sandbox/{meetingId}/join", UriKind.Relative),
            TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await instructorClient.GetAsync(
            new Uri($"/api/v1/meetings/sandbox/{meetingId}/host", UriKind.Relative),
            TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.Conflict);

        // Rol URL'sini degistirmek yetki kazandirmaz.
        (await context.Client.GetAsync(
            new Uri($"/api/v1/meetings/sandbox/{meetingId}/host", UriKind.Relative),
            TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await instructorClient.GetAsync(
            new Uri($"/api/v1/meetings/sandbox/{meetingId}/join", UriKind.Relative),
            TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var instructorSchedule = await instructorClient.GetFromJsonAsync<
            IReadOnlyList<InstructorScheduleListItem>>(
            new Uri("/api/v1/instructors/me/schedule", UriKind.Relative),
            TestJson.Options,
            TestContext.Current.CancellationToken);
        var scheduledLesson = instructorSchedule.ShouldHaveSingleItem();
        scheduledLesson.SessionId.ShouldBe(opened.SessionId);
        scheduledLesson.MeetingProvider.ShouldBe("sandbox");
        scheduledLesson.MeetingReference.ShouldBeNull();
        scheduledLesson.Learners.ShouldHaveSingleItem().FirstName.ShouldBe("Deniz");
        scheduledLesson.Learners.ShouldHaveSingleItem().LastName.ShouldBe("Yilmaz");

        var occupiedSlots = await context.Client.GetFromJsonAsync<
            IReadOnlyList<InstructorSlotListItem>>(
            slotsUri,
            TestJson.Options,
            TestContext.Current.CancellationToken);
        // Rezerve edilen slot ogrenciye donen listeden tamamen cikar.
        occupiedSlots!.ShouldNotContain(slot => slot.SessionId == opened.SessionId);

        var bookings = await context.Client.GetFromJsonAsync<PagedResult<LearnerBookingListItem>>(
            new Uri("/api/v1/bookings/me", UriKind.Relative),
            TestJson.Options,
            TestContext.Current.CancellationToken);
        var learnerBooking = bookings!.Items.Single(item => item.Id == reservation.BookingId);
        learnerBooking.SessionId.ShouldBe(opened.SessionId);
        learnerBooking.MeetingProvider.ShouldBe("sandbox");
        // Ders yedi gun sonra: katilim baglantisi bes dakikalik pencere acilmadan sizmaz.
        learnerBooking.MeetingReference.ShouldBeNull();

        var cancelled = await context.Client.DeleteAsync(
            new Uri($"/api/v1/bookings/{reservation.BookingId}", UriKind.Relative),
            TestContext.Current.CancellationToken);
        cancelled.StatusCode.ShouldBe(HttpStatusCode.OK);

        await using (var database = fixture.CreateContext())
        {
            var movements = await database.CreditLedger
                .Where(item => item.BookingId == reservation.BookingId)
                .ToListAsync(TestContext.Current.CancellationToken);
            movements.Sum(item => item.Quantity).ShouldBe(0);
            movements.ShouldContain(item =>
                item.TransactionType == Domain.Billing.CreditTransactionType.Refund);

            var cancelledMeeting = await database.Meetings.SingleAsync(
                item => item.Id == meetingId,
                TestContext.Current.CancellationToken);
            cancelledMeeting.Status.ShouldBe(Domain.Scheduling.MeetingStatus.Cancelled);
            cancelledMeeting.ProviderCancelledAt.ShouldBeNull();
        }

        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var processor = scope.ServiceProvider
                .GetRequiredService<IMeetingProvisioningProcessor>();
            (await processor.ProcessCancellationBatchAsync(
                TestContext.Current.CancellationToken)).ShouldBeGreaterThanOrEqualTo(1);
        }

        await using (var database = fixture.CreateContext())
        {
            var cancelledMeeting = await database.Meetings.SingleAsync(
                item => item.Id == meetingId,
                TestContext.Current.CancellationToken);
            cancelledMeeting.ProviderCancelledAt.ShouldNotBeNull();
            cancelledMeeting.CancellationAttemptCount.ShouldBe(1);
        }

        var slotsAfterCancellation = await context.Client.GetFromJsonAsync<
            IReadOnlyList<InstructorSlotListItem>>(
            slotsUri,
            TestJson.Options,
            TestContext.Current.CancellationToken);
        slotsAfterCancellation.ShouldBeEmpty();

        var secondResponse = await instructorClient.PostAsJsonAsync(
            new Uri("/api/v1/instructors/me/slots", UriKind.Relative),
            new
            {
                courseId,
                startsAt = localStart.AddHours(2),
                lessonDurationMinutes = 50
            },
            TestContext.Current.CancellationToken);
        var second = await secondResponse.Content.ReadFromJsonAsync<OpenInstructorSlotResult>(
            TestJson.Options,
            TestContext.Current.CancellationToken);

        (await instructorClient.DeleteAsync(
            new Uri($"/api/v1/instructors/me/slots/{second!.SessionId}", UriKind.Relative),
            TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        context.Dispose();
    }

    [Fact]
    public async Task InstructorSlot_ClosesToBookingThirtyMinutesBeforeStart()
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

        using var instructorClient = fixture.CreateClient();
        await SignIn(instructorClient, "ogretmen@hotmail.com", "ogretmen123");
        instructorClient.DefaultRequestHeaders.Add(
            OrganizationHeader, context.OrganizationId.ToString());

        // Baslangica yirmi dakika kalan slot hic acilamaz: acilsaydi pencere on
        // dakika once kapanmis olacak, yani slot ogrenciye hic gorunmeyecekti.
        var tooSoonResponse = await instructorClient.PostAsJsonAsync(
            new Uri("/api/v1/instructors/me/slots", UriKind.Relative),
            new { courseId, startsAt = DateTimeOffset.UtcNow.AddMinutes(20) },
            TestContext.Current.CancellationToken);
        tooSoonResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await tooSoonResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken))
            .ShouldContain("scheduling.slot_too_soon");

        // Baslangica iki saat kalan slot: pencere hala acik.
        var openStart = DateTimeOffset.UtcNow.AddHours(2);
        var openResponse = await instructorClient.PostAsJsonAsync(
            new Uri("/api/v1/instructors/me/slots", UriKind.Relative),
            new { courseId, startsAt = openStart },
            TestContext.Current.CancellationToken);
        openResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var openSlot = await openResponse.Content.ReadFromJsonAsync<OpenInstructorSlotResult>(
            TestJson.Options,
            TestContext.Current.CancellationToken);

        // Pencere kapanmis bir slot artik API ile uretilemiyor. Zaman ilerledikce
        // olusan bu durumu taklit etmek icin gecerli bir slotun kapanisi geriye cekilir.
        var closedStart = DateTimeOffset.UtcNow.AddHours(3);
        var closedResponse = await instructorClient.PostAsJsonAsync(
            new Uri("/api/v1/instructors/me/slots", UriKind.Relative),
            new { courseId, startsAt = closedStart },
            TestContext.Current.CancellationToken);
        closedResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var closedSlot = await closedResponse.Content.ReadFromJsonAsync<OpenInstructorSlotResult>(
            TestJson.Options,
            TestContext.Current.CancellationToken);

        await using (var db = fixture.CreateContext())
        {
            var stored = await db.LessonSessions.SingleAsync(
                session => session.Id == closedSlot!.SessionId,
                TestContext.Current.CancellationToken);
            db.Entry(stored).Property(session => session.BookingClosesAt)
                .CurrentValue = DateTimeOffset.UtcNow.AddMinutes(-5);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var rangeStart = DateTimeOffset.UtcNow.AddMinutes(-5);
        var slotsUri = new Uri(
            $"/api/v1/instructors/{profileId}/slots?courseId={courseId}"
            + $"&from={Uri.EscapeDataString(rangeStart.ToString("O"))}"
            + $"&until={Uri.EscapeDataString(rangeStart.AddDays(1).ToString("O"))}",
            UriKind.Relative);

        var visibleSlots = await context.Client.GetFromJsonAsync<
            IReadOnlyList<InstructorSlotListItem>>(
            slotsUri,
            TestJson.Options,
            TestContext.Current.CancellationToken);

        visibleSlots!.ShouldNotContain(slot => slot.SessionId == closedSlot!.SessionId);
        var listedOpenSlot = visibleSlots.ShouldHaveSingleItem();
        listedOpenSlot.SessionId.ShouldBe(openSlot!.SessionId);
        listedOpenSlot.BookingClosesAt.ShouldNotBeNull();
        listedOpenSlot.BookingClosesAt!.Value
            .ShouldBe(openSlot.StartsAt.AddMinutes(-30), TimeSpan.FromSeconds(1));

        // Eski sayfayi acik birakan kullaniciyi API reddeder.
        var rejected = await context.Client.PostAsJsonAsync(
            new Uri($"/api/v1/sessions/{closedSlot!.SessionId}/bookings", UriKind.Relative),
            new { learnerUserId = (Guid?)null, lessonDurationMinutes = 50 },
            TestContext.Current.CancellationToken);
        rejected.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        var accepted = await context.Client.PostAsJsonAsync(
            new Uri($"/api/v1/sessions/{openSlot.SessionId}/bookings", UriKind.Relative),
            new { learnerUserId = (Guid?)null, lessonDurationMinutes = 50 },
            TestContext.Current.CancellationToken);
        accepted.StatusCode.ShouldBe(
            HttpStatusCode.OK,
            await accepted.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        context.Dispose();
    }

    [Fact]
    public async Task InstructorSlot_ShouldUsePackageDuration_AndRejectIncompatibleBooking()
    {
        var context = await NewOrganization();
        var (courseId, subjectId) = await CreatePublishedPrivateCourse(
            context.Client,
            lessonDurationMinutes: 30);
        var profileId = await CreateInstructor(context);

        (await context.Client.PostAsync(
            new Uri($"/api/v1/instructors/{profileId}/activate", UriKind.Relative),
            content: null,
            TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await context.Client.PostAsJsonAsync(
            new Uri($"/api/v1/instructors/{profileId}/subjects", UriKind.Relative),
            new { subjectId, levelId = (Guid?)null },
            TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.OK);

        using var instructorClient = fixture.CreateClient();
        await SignIn(instructorClient, "ogretmen@hotmail.com", "ogretmen123");
        instructorClient.DefaultRequestHeaders.Add(
            OrganizationHeader, context.OrganizationId.ToString());

        // Egitmen her zaman bir saatlik slot acar; sure rezervasyonda paketin
        // ders suresine daralir.
        var startsAt = DateTimeOffset.UtcNow.AddDays(10);
        var slotResponse = await instructorClient.PostAsJsonAsync(
            new Uri("/api/v1/instructors/me/slots", UriKind.Relative),
            new { courseId, startsAt },
            TestContext.Current.CancellationToken);
        slotResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var slot = await slotResponse.Content
            .ReadFromJsonAsync<OpenInstructorSlotResult>(
                TestJson.Options,
                TestContext.Current.CancellationToken);

        slot!.LessonDurationMinutes.ShouldBe(60);
        (slot.EndsAt - slot.StartsAt).ShouldBe(TimeSpan.FromMinutes(60));

        var bookingResponse = await context.Client.PostAsJsonAsync(
            new Uri($"/api/v1/sessions/{slot.SessionId}/bookings", UriKind.Relative),
            new { learnerUserId = (Guid?)null, lessonDurationMinutes = 30 },
            TestContext.Current.CancellationToken);
        bookingResponse.StatusCode.ShouldBe(
            HttpStatusCode.OK,
            await bookingResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        await using (var database = fixture.CreateContext())
        {
            var stored = await database.LessonSessions.SingleAsync(
                item => item.Id == slot.SessionId,
                TestContext.Current.CancellationToken);
            (stored.EndsAt - stored.StartsAt).ShouldBe(TimeSpan.FromMinutes(30));
        }

        var secondSlotResponse = await instructorClient.PostAsJsonAsync(
            new Uri("/api/v1/instructors/me/slots", UriKind.Relative),
            new { courseId, startsAt = startsAt.AddHours(2) },
            TestContext.Current.CancellationToken);
        secondSlotResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var secondSlot = await secondSlotResponse.Content
            .ReadFromJsonAsync<OpenInstructorSlotResult>(
                TestJson.Options,
                TestContext.Current.CancellationToken);

        // Otuz dakikalik paket elli dakikalik rezervasyonu karsilamaz.
        var incompatibleBooking = await context.Client.PostAsJsonAsync(
            new Uri($"/api/v1/sessions/{secondSlot!.SessionId}/bookings", UriKind.Relative),
            new { learnerUserId = (Guid?)null, lessonDurationMinutes = 50 },
            TestContext.Current.CancellationToken);
        incompatibleBooking.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        // Birebir derste yalnizca 30 ve 50 dakika gecerlidir.
        var invalidDuration = await context.Client.PostAsJsonAsync(
            new Uri($"/api/v1/sessions/{secondSlot.SessionId}/bookings", UriKind.Relative),
            new { learnerUserId = (Guid?)null, lessonDurationMinutes = 45 },
            TestContext.Current.CancellationToken);
        invalidDuration.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

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
        await PurchasePackage(client, subjectId);

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
        HttpClient client,
        int lessonDurationMinutes = 50)
    {
        var subject = await client.PostAsJsonAsync(
            new Uri("/api/v1/subjects", UriKind.Relative),
            new CreateSubjectCommand("Ingilizce", $"alan-{Guid.CreateVersion7():N}"[..20], null),
            TestContext.Current.CancellationToken);
        var subjectId = (await subject.Content.ReadFromJsonAsync<CreateSubjectResult>(
            TestContext.Current.CancellationToken))!.SubjectId;
        await PurchasePackage(client, subjectId, lessonDurationMinutes);

        var course = await client.PostAsJsonAsync(
            new Uri("/api/v1/courses", UriKind.Relative),
            new
            {
                subjectId,
                title = "Birebir Ingilizce",
                courseType = "Private",
                defaultDurationMinutes = 50,
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

    private static async Task PurchasePackage(
        HttpClient client,
        Guid subjectId,
        int lessonDurationMinutes = 50)
    {
        var response = await client.PostAsJsonAsync(
            new Uri("/api/v1/subscriptions/demo-purchases", UriKind.Relative),
            new
            {
                subjectId,
                lessonsPerWeek = 3,
                durationMonths = 6,
                lessonDurationMinutes
            },
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task EndedSession_IsCompletedAutomatically_WithoutInstructorAction()
    {
        var context = await NewOrganization();
        var (courseId, subjectId) = await CreatePublishedPrivateCourse(context.Client);

        (await context.Client.PutAsJsonAsync(
            new Uri("/api/v1/admin/compensation/rates", UriKind.Relative),
            new { subjectId, lessonDurationMinutes = 50, amount = 400m, currency = "TRY" },
            TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.OK);

        var profileId = await CreateInstructor(context);

        (await context.Client.PostAsync(
            new Uri($"/api/v1/instructors/{profileId}/activate", UriKind.Relative),
            content: null,
            TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await context.Client.PostAsJsonAsync(
            new Uri($"/api/v1/instructors/{profileId}/subjects", UriKind.Relative),
            new { subjectId, levelId = (Guid?)null },
            TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.OK);

        using var instructorClient = fixture.CreateClient();
        await SignIn(instructorClient, "ogretmen@hotmail.com", "ogretmen123");
        instructorClient.DefaultRequestHeaders.Add(
            OrganizationHeader, context.OrganizationId.ToString());

        var openedResponse = await instructorClient.PostAsJsonAsync(
            new Uri("/api/v1/instructors/me/slots", UriKind.Relative),
            new { courseId, startsAt = DateTimeOffset.UtcNow.AddDays(2), lessonDurationMinutes = 50 },
            TestContext.Current.CancellationToken);
        openedResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var opened = await openedResponse.Content.ReadFromJsonAsync<OpenInstructorSlotResult>(
            TestJson.Options,
            TestContext.Current.CancellationToken);

        var bookingResponse = await context.Client.PostAsJsonAsync(
            new Uri($"/api/v1/sessions/{opened!.SessionId}/bookings", UriKind.Relative),
            new { learnerUserId = (Guid?)null, lessonDurationMinutes = 50 },
            TestContext.Current.CancellationToken);
        bookingResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var booking = await bookingResponse.Content.ReadFromJsonAsync<CreateBookingResult>(
            TestJson.Options,
            TestContext.Current.CancellationToken);

        // Slot yalnizca gelecege acilabildigi icin ders bitmis hale getirilir.
        await using (var database = fixture.CreateContext())
        {
            var now = DateTimeOffset.UtcNow;
            await database.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE lesson_sessions SET starts_at = {now.AddMinutes(-60)}, ends_at = {now.AddMinutes(-10)} WHERE id = {opened.SessionId}",
                TestContext.Current.CancellationToken);
        }

        // Egitmenden hicbir istek gelmeden, arka plan islemi dersi kapatir.
        var result = await RunSessionCompletionAsync();
        result.CompletedSessions.ShouldBe(1);
        result.CompletedBookings.ShouldBe(1);

        await using (var database = fixture.CreateContext())
        {
            (await database.LessonSessions.SingleAsync(
                item => item.Id == opened.SessionId,
                TestContext.Current.CancellationToken)).Status
                .ShouldBe(Domain.Scheduling.LessonSessionStatus.Completed);

            (await database.SessionBookings.SingleAsync(
                item => item.Id == booking!.BookingId,
                TestContext.Current.CancellationToken)).Status
                .ShouldBe(Domain.Scheduling.BookingStatus.Attended);

            var attendance = await database.SessionAttendances.SingleAsync(
                item => item.BookingId == booking!.BookingId,
                TestContext.Current.CancellationToken);
            attendance.Status.ShouldBe(Domain.Scheduling.AttendanceStatus.Present);
            attendance.AttendedMinutes.ShouldBe(50);
            // Otomatik yoklamanin elle girilenden ayirt edilebilmesi icin bos kalir.
            attendance.MarkedByUserId.ShouldBeNull();

            (await database.InstructorEarnings.AnyAsync(
                item => item.SessionId == opened.SessionId,
                TestContext.Current.CancellationToken)).ShouldBeTrue();
        }

        // Tekrar calistiginda ayni oturumu yeniden islemez.
        (await RunSessionCompletionAsync()).CompletedSessions.ShouldBe(0);
    }

    /// <summary>
    /// Arka plan isini worker beklemeden calistirir. Kapsamda kiraci yoktur —
    /// uretimdeki calisma sekliyle ayni.
    /// </summary>
    private async Task<SessionCompletionResult> RunSessionCompletionAsync()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var processor = scope.ServiceProvider.GetRequiredService<ISessionCompletionProcessor>();
        return await processor.ProcessDueAsync(
            batchSize: 100,
            gracePeriod: TimeSpan.Zero,
            TestContext.Current.CancellationToken);
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
