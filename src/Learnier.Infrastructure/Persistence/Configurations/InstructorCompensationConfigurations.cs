using Learnier.Domain.Billing;
using Learnier.Domain.Catalog;
using Learnier.Domain.Scheduling;
using Learnier.Domain.Teaching;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Learnier.Infrastructure.Persistence.Configurations;

internal sealed class InstructorCompensationRateConfiguration
    : IEntityTypeConfiguration<InstructorCompensationRate>
{
    public void Configure(EntityTypeBuilder<InstructorCompensationRate> builder)
    {
        builder.ToTable("instructor_compensation_rates");
        builder.HasKey(rate => rate.Id);
        builder.Property(rate => rate.Amount).HasPrecision(18, 2).IsRequired();
        builder.Property(rate => rate.Currency).HasMaxLength(3).IsRequired();
        builder.HasIndex(rate => new
        {
            rate.OrganizationId,
            rate.SubjectId,
            rate.LessonDurationMinutes
        }).IsUnique();
        builder.HasOne<Subject>().WithMany().HasForeignKey(rate => rate.SubjectId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.ToTable(table =>
        {
            table.HasCheckConstraint("ck_instructor_compensation_duration", "lesson_duration_minutes IN (30, 50)");
            table.HasCheckConstraint("ck_instructor_compensation_amount", "amount >= 0");
        });
        builder.Ignore(rate => rate.DomainEvents);
    }
}

internal sealed class InstructorPenaltyStepConfiguration
    : IEntityTypeConfiguration<InstructorPenaltyStep>
{
    public void Configure(EntityTypeBuilder<InstructorPenaltyStep> builder)
    {
        builder.ToTable("instructor_penalty_steps");
        builder.HasKey(step => step.Id);
        builder.Property(step => step.Percentage).HasPrecision(5, 2).IsRequired();
        builder.HasIndex(step => new { step.OrganizationId, step.Level }).IsUnique();
        builder.ToTable(table =>
        {
            table.HasCheckConstraint("ck_instructor_penalty_level", "level > 0");
            table.HasCheckConstraint("ck_instructor_penalty_percentage", "percentage >= 0 AND percentage <= 100");
        });
    }
}

internal sealed class InstructorPenaltyStateConfiguration
    : IEntityTypeConfiguration<InstructorPenaltyState>
{
    public void Configure(EntityTypeBuilder<InstructorPenaltyState> builder)
    {
        builder.ToTable("instructor_penalty_states");
        builder.HasKey(state => state.Id);
        builder.HasIndex(state => state.InstructorProfileId).IsUnique();
        builder.HasOne<InstructorProfile>().WithMany()
            .HasForeignKey(state => state.InstructorProfileId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<LessonSession>().WithMany()
            .HasForeignKey(state => state.LastCancelledSessionId)
            .OnDelete(DeleteBehavior.SetNull);
        builder.ToTable(table => table.HasCheckConstraint(
            "ck_instructor_penalty_state_level", "level >= 0"));
    }
}

internal sealed class InstructorEarningConfiguration
    : IEntityTypeConfiguration<InstructorEarning>
{
    public void Configure(EntityTypeBuilder<InstructorEarning> builder)
    {
        builder.ToTable("instructor_earnings");
        builder.HasKey(earning => earning.Id);
        builder.Property(earning => earning.GrossAmount).HasPrecision(18, 2).IsRequired();
        builder.Property(earning => earning.PenaltyPercentage).HasPrecision(5, 2).IsRequired();
        builder.Property(earning => earning.PenaltyAmount).HasPrecision(18, 2).IsRequired();
        builder.Property(earning => earning.NetAmount).HasPrecision(18, 2).IsRequired();
        builder.Property(earning => earning.Currency).HasMaxLength(3).IsRequired();
        builder.HasIndex(earning => new { earning.SessionId, earning.InstructorProfileId }).IsUnique();
        builder.HasIndex(earning => new { earning.InstructorProfileId, earning.EarnedAt });
        builder.HasOne<LessonSession>().WithMany().HasForeignKey(earning => earning.SessionId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<InstructorProfile>().WithMany().HasForeignKey(earning => earning.InstructorProfileId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Subject>().WithMany().HasForeignKey(earning => earning.SubjectId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.ToTable(table =>
        {
            table.HasCheckConstraint("ck_instructor_earning_amounts", "gross_amount >= 0 AND penalty_amount >= 0 AND net_amount >= 0");
            table.HasCheckConstraint("ck_instructor_earning_penalty", "penalty_percentage >= 0 AND penalty_percentage <= 100");
        });
    }
}
