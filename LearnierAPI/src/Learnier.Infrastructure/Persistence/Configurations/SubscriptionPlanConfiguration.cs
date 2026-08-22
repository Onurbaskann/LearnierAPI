using Learnier.Domain.Billing;
using Learnier.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Learnier.Infrastructure.Persistence.Configurations;

internal sealed class SubscriptionPlanConfiguration : IEntityTypeConfiguration<SubscriptionPlan>
{
    public void Configure(EntityTypeBuilder<SubscriptionPlan> builder)
    {
        builder.ToTable("subscription_plans");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Name).HasMaxLength(200).IsRequired();
        builder.Property(p => p.Description).HasMaxLength(4000);

        builder.Property(p => p.CatalogAccess)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(p => p.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(p => p.MonthlyLessonCredits);
        builder.Property(p => p.LessonDurationMinutes);

        builder.HasIndex(p => new { p.OrganizationId, p.Status });

        builder.ToTable(t =>
        {
            t.HasCheckConstraint(
                "ck_subscription_plans_lesson_package_complete",
                "(monthly_lesson_credits IS NULL AND lesson_duration_minutes IS NULL) OR "
                + "(monthly_lesson_credits IS NOT NULL AND lesson_duration_minutes IS NOT NULL)");
            t.HasCheckConstraint(
                "ck_subscription_plans_monthly_credits_positive",
                "monthly_lesson_credits IS NULL OR monthly_lesson_credits > 0");
            t.HasCheckConstraint(
                "ck_subscription_plans_lesson_duration",
                "lesson_duration_minutes IS NULL OR lesson_duration_minutes IN (30, 50)");
        });

        builder.HasOne<Organization>()
            .WithMany()
            .HasForeignKey(p => p.OrganizationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(p => p.Prices)
            .WithOne(pr => pr.Plan)
            .HasForeignKey(pr => pr.PlanId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(p => p.Entitlements)
            .WithOne(e => e.Plan)
            .HasForeignKey(e => e.PlanId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Metadata
            .FindNavigation(nameof(SubscriptionPlan.Prices))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.Metadata
            .FindNavigation(nameof(SubscriptionPlan.Entitlements))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.Ignore(p => p.DomainEvents);
    }
}
