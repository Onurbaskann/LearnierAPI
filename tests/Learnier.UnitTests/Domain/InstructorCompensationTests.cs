using Learnier.Domain.Billing;
using Shouldly;

namespace Learnier.UnitTests.Domain;

public sealed class InstructorCompensationTests
{
    [Fact]
    public void Earning_ShouldApplyPenaltyToGrossAmount()
    {
        var earning = InstructorEarning.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 50,
            grossAmount: 100m,
            penaltyPercentage: 15m,
            currency: "try",
            DateTimeOffset.UtcNow);

        earning.GrossAmount.ShouldBe(100m);
        earning.PenaltyAmount.ShouldBe(15m);
        earning.NetAmount.ShouldBe(85m);
        earning.Currency.ShouldBe("TRY");
    }

    [Fact]
    public void PenaltyState_ShouldIncreaseAndResetAfterCompletedLesson()
    {
        var state = InstructorPenaltyState.Create(Guid.NewGuid());

        state.RegisterLateCancellation(Guid.NewGuid(), 10m, DateTimeOffset.UtcNow);
        state.RegisterLateCancellation(Guid.NewGuid(), 15m, DateTimeOffset.UtcNow);
        state.Level.ShouldBe(2);
        state.PendingPercentage.ShouldBe(15m);

        state.Clear();
        state.Level.ShouldBe(0);
        state.PendingPercentage.ShouldBeNull();
        state.LastCancelledSessionId.ShouldBeNull();
    }

    [Fact]
    public void PenaltyEvents_ShouldKeepSnapshotAndWaiverReason()
    {
        var organizationId = Guid.NewGuid();
        var instructorId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var occurredAt = DateTimeOffset.UtcNow;

        var late = InstructorPenaltyEvent.LateCancellation(
            organizationId, instructorId, Guid.NewGuid(), 2, 15m, occurredAt);
        var waived = InstructorPenaltyEvent.Waived(
            organizationId, instructorId, 2, 15m,
            "Sağlık belgesi doğrulandı", occurredAt.AddMinutes(5), actorId);

        late.EventType.ShouldBe(InstructorPenaltyEventType.LateCancellation);
        late.Level.ShouldBe(2);
        late.Percentage.ShouldBe(15m);
        waived.EventType.ShouldBe(InstructorPenaltyEventType.Waived);
        waived.Reason.ShouldBe("Sağlık belgesi doğrulandı");
        waived.ActorUserId.ShouldBe(actorId);
    }

    [Fact]
    public void PenaltyState_ShouldIgnoreSameCancelledSessionTwice()
    {
        var state = InstructorPenaltyState.Create(Guid.NewGuid());
        var sessionId = Guid.NewGuid();

        state.RegisterLateCancellation(sessionId, 10m, DateTimeOffset.UtcNow);
        state.RegisterLateCancellation(sessionId, 10m, DateTimeOffset.UtcNow.AddMinutes(1));

        state.Level.ShouldBe(1);
    }

    [Fact]
    public void PenaltyState_ShouldStayAtConfiguredMaximumLevel()
    {
        var state = InstructorPenaltyState.Create(Guid.NewGuid());

        state.RegisterLateCancellation(
            Guid.NewGuid(), 10m, DateTimeOffset.UtcNow, maximumLevel: 2);
        state.RegisterLateCancellation(
            Guid.NewGuid(), 15m, DateTimeOffset.UtcNow, maximumLevel: 2);
        state.RegisterLateCancellation(
            Guid.NewGuid(), 15m, DateTimeOffset.UtcNow, maximumLevel: 2);

        state.Level.ShouldBe(2);
        state.PendingPercentage.ShouldBe(15m);
    }

    [Theory]
    [InlineData(30)]
    [InlineData(50)]
    public void CompensationRate_ShouldSupportPackageDurations(int duration)
    {
        var rate = InstructorCompensationRate.Create(
            Guid.NewGuid(), Guid.NewGuid(), duration, 400m, "usd");

        rate.LessonDurationMinutes.ShouldBe(duration);
        rate.Currency.ShouldBe("USD");
    }
}
