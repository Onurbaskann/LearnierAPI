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
        entitlement.LessonDurationMinutes.ShouldBe(durationMinutes);
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
        entitlement.LessonDurationMinutes.ShouldBe(30);
    }

    /// <summary>
    /// Rezervasyon yetkilendirmesi ders suresini hak tanimindan okur; suresiz bir
    /// birebir ders kredisi hicbir oturumla eslesmeyecegi icin bastan reddedilir.
    /// </summary>
    [Fact]
    public void AddEntitlement_ShouldRequireLessonDurationForPrivateCredit()
    {
        var plan = SubscriptionPlan.Create(Guid.NewGuid(), "Plan", CatalogAccess.All);

        Should.Throw<ArgumentException>(() => plan.AddEntitlement(
            EntitlementType.LessonCredit,
            SessionType.Private,
            quantity: 4,
            EntitlementResetPeriod.Month,
            lessonDurationMinutes: null));
    }

    [Theory]
    [InlineData(25)]
    [InlineData(45)]
    [InlineData(60)]
    public void AddEntitlement_ShouldRejectUnsupportedLessonDuration(int durationMinutes)
    {
        var plan = SubscriptionPlan.Create(Guid.NewGuid(), "Plan", CatalogAccess.All);

        Should.Throw<ArgumentOutOfRangeException>(() => plan.AddEntitlement(
            EntitlementType.LessonCredit,
            SessionType.Private,
            quantity: 4,
            EntitlementResetPeriod.Month,
            durationMinutes));
    }

    /// <summary>Grup ve webinar oturumlari sure kirilimiyla satilmiyor.</summary>
    [Theory]
    [InlineData(EntitlementType.LessonCredit, SessionType.Group)]
    [InlineData(EntitlementType.BookingAccess, SessionType.Private)]
    public void AddEntitlement_ShouldRejectLessonDurationOutsidePrivateCredit(
        EntitlementType entitlementType,
        SessionType sessionType)
    {
        var plan = SubscriptionPlan.Create(Guid.NewGuid(), "Plan", CatalogAccess.All);
        var quantity = entitlementType is EntitlementType.LessonCredit ? (int?)4 : null;

        Should.Throw<ArgumentException>(() => plan.AddEntitlement(
            entitlementType,
            sessionType,
            quantity,
            EntitlementResetPeriod.Month,
            lessonDurationMinutes: 50));
    }

    /// <summary>
    /// Ayni plan hem 30 hem 50 dakikalik birebir ders kredisi tasiyabilir; ikisi
    /// farkli oturum sureleriyle eslesir.
    /// </summary>
    [Fact]
    public void AddEntitlement_ShouldAllowBothPrivateLessonDurations()
    {
        var plan = SubscriptionPlan.Create(Guid.NewGuid(), "Plan", CatalogAccess.All);

        plan.AddEntitlement(
            EntitlementType.LessonCredit, SessionType.Private, 4, EntitlementResetPeriod.Month, 30);
        plan.AddEntitlement(
            EntitlementType.LessonCredit, SessionType.Private, 8, EntitlementResetPeriod.Month, 50);

        plan.Entitlements.Count.ShouldBe(2);
    }
}
