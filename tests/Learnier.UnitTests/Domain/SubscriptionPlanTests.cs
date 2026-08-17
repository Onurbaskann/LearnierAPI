using Learnier.Domain.Billing;
using Learnier.Domain.Scheduling;
using Shouldly;

namespace Learnier.UnitTests.Domain;

public sealed class SubscriptionPlanTests
{
    [Theory]
    [InlineData(30)]
    [InlineData(50)]
    public void CreateLessonPackage_ShouldCreateMonthlyPrivateLessonEntitlement(int durationMinutes)
    {
        var plan = SubscriptionPlan.CreateLessonPackage(
            Guid.NewGuid(),
            "Ingilizce Paketi",
            monthlyLessonCredits: 12,
            lessonDurationMinutes: durationMinutes);

        plan.IsLessonPackage.ShouldBeTrue();
        plan.CatalogAccess.ShouldBe(CatalogAccess.Restricted);
        plan.MonthlyLessonCredits.ShouldBe(12);
        plan.LessonDurationMinutes.ShouldBe(durationMinutes);

        var entitlement = plan.Entitlements.ShouldHaveSingleItem();
        entitlement.EntitlementType.ShouldBe(EntitlementType.LessonCredit);
        entitlement.SessionType.ShouldBe(SessionType.Private);
        entitlement.Quantity.ShouldBe(12);
        entitlement.ResetPeriod.ShouldBe(EntitlementResetPeriod.Month);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void CreateLessonPackage_ShouldRejectNonPositiveMonthlyCredits(int monthlyCredits)
    {
        Should.Throw<ArgumentOutOfRangeException>(() =>
            SubscriptionPlan.CreateLessonPackage(
                Guid.NewGuid(),
                "Ingilizce Paketi",
                monthlyCredits,
                lessonDurationMinutes: 50));
    }

    [Theory]
    [InlineData(25)]
    [InlineData(45)]
    [InlineData(60)]
    public void CreateLessonPackage_ShouldRejectUnsupportedLessonDuration(int durationMinutes)
    {
        Should.Throw<ArgumentOutOfRangeException>(() =>
            SubscriptionPlan.CreateLessonPackage(
                Guid.NewGuid(),
                "Ingilizce Paketi",
                monthlyLessonCredits: 12,
                lessonDurationMinutes: durationMinutes));
    }

    [Fact]
    public void ConfigureLessonPackage_ShouldBeIdempotentButImmutable()
    {
        var plan = SubscriptionPlan.Create(
            Guid.NewGuid(),
            "Eski Paket",
            CatalogAccess.Restricted);

        plan.ConfigureLessonPackage(8, 30);
        plan.ConfigureLessonPackage(8, 30);

        Should.Throw<InvalidOperationException>(() => plan.ConfigureLessonPackage(12, 50));
    }

    [Fact]
    public void ConfigureLessonPackage_ShouldAddMonthlyEntitlementToLegacyPlan()
    {
        var plan = SubscriptionPlan.Create(
            Guid.NewGuid(),
            "Eski Paket",
            CatalogAccess.Restricted);

        plan.ConfigureLessonPackage(8, 30);

        var entitlement = plan.Entitlements.ShouldHaveSingleItem();
        entitlement.Quantity.ShouldBe(8);
        entitlement.ResetPeriod.ShouldBe(EntitlementResetPeriod.Month);
    }
}
