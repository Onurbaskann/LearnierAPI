using Learnier.Domain.Progress;
using Learnier.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Learnier.Infrastructure.Persistence.Configurations;

internal sealed class LearnerOnboardingProfileConfiguration
    : IEntityTypeConfiguration<LearnerOnboardingProfile>
{
    public void Configure(EntityTypeBuilder<LearnerOnboardingProfile> builder)
    {
        builder.ToTable("learner_onboarding_profiles");
        builder.HasKey(profile => profile.Id);
        builder.HasIndex(profile => new { profile.OrganizationId, profile.LearnerUserId, profile.SubjectId })
            .IsUnique();

        builder.Property(profile => profile.LearningGoal).HasConversion<string>().HasMaxLength(32);
        builder.Property(profile => profile.SelfAssessment).HasConversion<string>().HasMaxLength(32);
        builder.Property(profile => profile.LessonFocus).HasConversion<string>().HasMaxLength(32);
        builder.Property(profile => profile.InstructorPreference).HasConversion<string>().HasMaxLength(32);
        builder.Property(profile => profile.DifficultyAreas).HasColumnType("text[]").IsRequired();
        builder.Property(profile => profile.AvailabilityPreferences).HasColumnType("text[]").IsRequired();

        builder.HasOne(profile => profile.Subject)
            .WithMany()
            .HasForeignKey(profile => profile.SubjectId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(profile => profile.LearnerUserId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Organization>()
            .WithMany()
            .HasForeignKey(profile => profile.OrganizationId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(profile => profile.EstimatedLevel)
            .WithMany()
            .HasForeignKey(profile => profile.EstimatedLevelId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.ToTable(table => table.HasCheckConstraint(
            "ck_learner_onboarding_profiles_weekly_goal",
            "weekly_lesson_goal >= 1 AND weekly_lesson_goal <= 7"));
    }
}
