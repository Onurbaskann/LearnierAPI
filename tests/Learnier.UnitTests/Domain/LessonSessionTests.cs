using Learnier.Domain.Scheduling;
using Shouldly;

namespace Learnier.UnitTests.Domain;

public sealed class LessonSessionTests
{
    [Theory]
    [InlineData(30)]
    [InlineData(50)]
    public void CreatePrivateSession_ShouldAcceptPackageDurations(int durationMinutes)
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
    [InlineData(60)]
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
}
