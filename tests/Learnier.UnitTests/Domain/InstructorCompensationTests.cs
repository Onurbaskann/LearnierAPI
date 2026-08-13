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

        state.RegisterLateCancellation(Guid.NewGuid(), DateTimeOffset.UtcNow);
        state.RegisterLateCancellation(Guid.NewGuid(), DateTimeOffset.UtcNow);
        state.Level.ShouldBe(2);

        state.Clear();
        state.Level.ShouldBe(0);
        state.LastCancelledSessionId.ShouldBeNull();
    }

    [Fact]
    public void PenaltyState_ShouldIgnoreSameCancelledSessionTwice()
    {
        var state = InstructorPenaltyState.Create(Guid.NewGuid());
        var sessionId = Guid.NewGuid();

        state.RegisterLateCancellation(sessionId, DateTimeOffset.UtcNow);
        state.RegisterLateCancellation(sessionId, DateTimeOffset.UtcNow.AddMinutes(1));

        state.Level.ShouldBe(1);
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
