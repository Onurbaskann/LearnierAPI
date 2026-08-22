using Learnier.Domain.Scheduling;
using Shouldly;

namespace Learnier.UnitTests.Domain;

public sealed class LessonSessionTests
{
    [Theory]
    [InlineData(30)]
    [InlineData(50)]
    [InlineData(60)]
    public void CreatePrivateSession_ShouldAcceptLessonAndSlotDurations(int durationMinutes)
    {
        var startsAt = DateTimeOffset.UtcNow.AddDays(1);

        var session = LessonSession.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            SessionType.Private,
            startsAt,
            startsAt.AddMinutes(durationMinutes),
            capacity: 1,
            minimumParticipants: 1);

        (session.EndsAt - session.StartsAt).ShouldBe(TimeSpan.FromMinutes(durationMinutes));
    }

    [Theory]
    [InlineData(25)]
    [InlineData(45)]
    public void CreatePrivateSession_ShouldRejectUnsupportedDuration(int durationMinutes)
    {
        var startsAt = DateTimeOffset.UtcNow.AddDays(1);

        Should.Throw<ArgumentOutOfRangeException>(() => LessonSession.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            SessionType.Private,
            startsAt,
            startsAt.AddMinutes(durationMinutes),
            capacity: 1,
            minimumParticipants: 1));
    }

    [Theory]
    [InlineData(30)]
    [InlineData(50)]
    public void ApplyPrivateLessonDuration_ShouldShrinkHourlySlot(int durationMinutes)
    {
        var startsAt = DateTimeOffset.UtcNow.AddDays(1);
        var session = LessonSession.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            SessionType.Private,
            startsAt,
            startsAt.AddHours(1),
            capacity: 1,
            minimumParticipants: 1);

        session.ApplyPrivateLessonDuration(durationMinutes);

        (session.EndsAt - session.StartsAt).ShouldBe(TimeSpan.FromMinutes(durationMinutes));
    }

    [Theory]
    // 16.30 baslangicli slot 16.00'da kapanir; kapanis ani dahil degildir.
    [InlineData(-1, true)]
    [InlineData(0, false)]
    [InlineData(1, false)]
    public void IsBookable_ShouldCloseThirtyMinutesBeforeStart(
        int ticksFromCutoff,
        bool expected)
    {
        var startsAt = new DateTimeOffset(2026, 8, 14, 16, 30, 0, TimeSpan.Zero);
        var session = LessonSession.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            SessionType.Private,
            startsAt,
            startsAt.AddHours(1),
            capacity: 1,
            minimumParticipants: 1);
        var closesAt = startsAt.AddMinutes(-LessonSession.BookingCutoffMinutes);
        session.SetBookingWindow(null, closesAt, null);

        session.IsBookable(closesAt.AddTicks(ticksFromCutoff)).ShouldBe(expected);
    }

    [Fact]
    public void IsBookable_ShouldBeFalseOnceSessionStarted()
    {
        var startsAt = new DateTimeOffset(2026, 8, 14, 16, 30, 0, TimeSpan.Zero);
        var session = LessonSession.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            SessionType.Private,
            startsAt,
            startsAt.AddHours(1),
            capacity: 1,
            minimumParticipants: 1);

        session.IsBookable(startsAt).ShouldBeFalse();
    }
}
