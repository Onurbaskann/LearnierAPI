using Learnier.Domain.Catalog;
using Learnier.Domain.Identity;
using Learnier.Domain.Scheduling;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Learnier.Infrastructure.Persistence.Configurations;

internal sealed class LessonSessionConfiguration : IEntityTypeConfiguration<LessonSession>
{
    public void Configure(EntityTypeBuilder<LessonSession> builder)
    {
        builder.ToTable("lesson_sessions");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.SessionType)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(s => s.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(s => s.MeetingProvider).HasMaxLength(32);
        builder.Property(s => s.MeetingReference).HasMaxLength(500);
        builder.Property(s => s.CancellationReason).HasMaxLength(500);

        // Takvim sorgusunun ana indexi: kurum + zaman araligi + durum.
        builder.HasIndex(s => new { s.OrganizationId, s.StartsAt, s.Status });
        builder.HasIndex(s => new { s.ClassGroupId, s.StartsAt });

        builder.HasOne<Organization>()
            .WithMany()
            .HasForeignKey(s => s.OrganizationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(s => s.Course)
            .WithMany()
            .HasForeignKey(s => s.CourseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ClassGroup>()
            .WithMany()
            .HasForeignKey(s => s.ClassGroupId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne<CourseLesson>()
            .WithMany()
            .HasForeignKey(s => s.CourseLessonId)
            // Mufredat konusu silinirse oturum kaydi kalir, konusu bosalir.
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(s => s.Instructors)
            .WithOne(i => i.Session)
            .HasForeignKey(i => i.SessionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(s => s.Bookings)
            .WithOne(b => b.Session)
            .HasForeignKey(b => b.SessionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Metadata
            .FindNavigation(nameof(LessonSession.Instructors))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.Metadata
            .FindNavigation(nameof(LessonSession.Bookings))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.ToTable(t =>
        {
            t.HasCheckConstraint("ck_lesson_sessions_time_range", @"ends_at > starts_at");

            t.HasCheckConstraint("ck_lesson_sessions_capacity_positive", @"capacity > 0");

            t.HasCheckConstraint(
                "ck_lesson_sessions_minimum_participants",
                @"minimum_participants >= 0 AND minimum_participants <= capacity");

            t.HasCheckConstraint(
                "ck_lesson_sessions_booking_window",
                @"booking_opens_at IS NULL
                  OR booking_closes_at IS NULL
                  OR booking_closes_at >= booking_opens_at");
        });

        builder.Ignore(s => s.DomainEvents);
    }
}
