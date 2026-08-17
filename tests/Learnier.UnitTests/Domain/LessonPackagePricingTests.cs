using Learnier.Domain.Billing;
using Shouldly;

namespace Learnier.UnitTests.Domain;

public sealed class LessonPackagePricingTests
{
    [Theory]
    [InlineData(30, 7200)]
    [InlineData(50, 12000)]
    public void CalculateTotal_ShouldPriceLessonDurationSeparately(
        int lessonDurationMinutes,
        decimal expectedTotal)
    {
        LessonPackagePricing.CalculateTotal(2, 6, lessonDurationMinutes)
            .ShouldBe(expectedTotal);
    }

    [Theory]
    [InlineData(30, 18360)]
    [InlineData(50, 30600)]
    public void CalculateTotal_ShouldApplyFrequencyAndCommitmentDiscounts(
        int lessonDurationMinutes,
        decimal expectedTotal)
    {
        LessonPackagePricing.CalculateTotal(3, 12, lessonDurationMinutes)
            .ShouldBe(expectedTotal);
    }

    [Fact]
    public void CalculateTotal_ShouldRejectUnsupportedDuration()
    {
        Should.Throw<ArgumentOutOfRangeException>(() =>
            LessonPackagePricing.CalculateTotal(2, 6, 45));
    }
}
