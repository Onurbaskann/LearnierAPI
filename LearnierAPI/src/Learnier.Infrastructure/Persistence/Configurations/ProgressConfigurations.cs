using Learnier.Domain.Progress;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Learnier.Infrastructure.Persistence.Configurations;

internal sealed class LearnerCourseProgressConfiguration : IEntityTypeConfiguration<LearnerCourseProgress>
{
    public void Configure(EntityTypeBuilder<LearnerCourseProgress> builder)
    {
        builder.ToTable("learner_course_progress");

        builder.HasKey(p => p.Id);

        // Oran yuzde olarak tutulur; iki ondalik hane yeterli hassasiyeti verir.
        builder.Property(p => p.CompletionPercentage).HasPrecision(5, 2).IsRequired();

        builder.HasIndex(p => new { p.LearnerUserId, p.CourseId }).IsUnique();

        builder.HasOne(p => p.Learner)
            .WithMany()
            .HasForeignKey(p => p.LearnerUserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(p => p.Course)
            .WithMany()
            .HasForeignKey(p => p.CourseId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(p => p.CurrentLevel)
            .WithMany()
            .HasForeignKey(p => p.CurrentLevelId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.ToTable(t => t.HasCheckConstraint(
            "ck_learner_course_progress_percentage_range",
            @"completion_percentage >= 0 AND completion_percentage <= 100"));
    }
}

internal sealed class LessonCompletionConfiguration : IEntityTypeConfiguration<LessonCompletion>
{
    public void Configure(EntityTypeBuilder<LessonCompletion> builder)
    {
        builder.ToTable("lesson_completions");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.CompletionSource)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        // Bir konu bir ogrenci icin bir kez tamamlanir.
        builder.HasIndex(c => new { c.LearnerUserId, c.CourseLessonId }).IsUnique();

        builder.HasOne(c => c.Learner)
            .WithMany()
            .HasForeignKey(c => c.LearnerUserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(c => c.CourseLesson)
            .WithMany()
            .HasForeignKey(c => c.CourseLessonId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(c => c.Session)
            .WithMany()
            .HasForeignKey(c => c.SessionId)
            // Oturum silinse de tamamlama kaydi korunur; ilerleme geriye gitmemeli.
            .OnDelete(DeleteBehavior.SetNull);
    }
}

internal sealed class SessionFeedbackConfiguration : IEntityTypeConfiguration<SessionFeedback>
{
    public void Configure(EntityTypeBuilder<SessionFeedback> builder)
    {
        builder.ToTable("session_feedback");

        builder.HasKey(f => f.Id);

        builder.Property(f => f.Comment).HasMaxLength(4000);

        // Bir kisi ayni oturumda ayni hedefe tek degerlendirme yazar. Hedef bos
        // olabildigi icin NULL'lar esit sayilir; aksi halde oturumun kendisine
        // defalarca puan verilebilirdi.
        builder.HasIndex(f => new { f.SessionId, f.AuthorUserId, f.TargetInstructorProfileId })
            .IsUnique()
            .AreNullsDistinct(false);

        builder.HasOne(f => f.Session)
            .WithMany()
            .HasForeignKey(f => f.SessionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(f => f.Author)
            .WithMany()
            .HasForeignKey(f => f.AuthorUserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(f => f.TargetInstructor)
            .WithMany()
            .HasForeignKey(f => f.TargetInstructorProfileId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.ToTable(t => t.HasCheckConstraint(
            "ck_session_feedback_rating_range",
            $"rating >= {SessionFeedback.MinRating} AND rating <= {SessionFeedback.MaxRating}"));
    }
}
