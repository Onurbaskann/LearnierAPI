using Learnier.Domain.Billing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Learnier.Infrastructure.Persistence.Configurations;

/// <remarks>
/// Iki erisim tablosu da yalnizca iki yabanci anahtardan olusan saf baglanti
/// tablolaridir; ayni dosyada tutulmalari okunurlugu artirir.
/// </remarks>
internal sealed class PlanSubjectAccessConfiguration : IEntityTypeConfiguration<PlanSubjectAccess>
{
    public void Configure(EntityTypeBuilder<PlanSubjectAccess> builder)
    {
        builder.ToTable("plan_subject_access");

        // Baglanti tablosunun dogal anahtari iki FK'dir; ayri bir Id gereksizdir.
        builder.HasKey(a => new { a.PlanId, a.SubjectId });

        // ADR-001: bir ders paketi yalnizca tek bir Subject'e satilir.
        builder.HasIndex(a => a.PlanId).IsUnique();

        builder.HasOne(a => a.Plan)
            .WithMany()
            .HasForeignKey(a => a.PlanId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(a => a.Subject)
            .WithMany()
            .HasForeignKey(a => a.SubjectId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class PlanCourseAccessConfiguration : IEntityTypeConfiguration<PlanCourseAccess>
{
    public void Configure(EntityTypeBuilder<PlanCourseAccess> builder)
    {
        builder.ToTable("plan_course_access");

        builder.HasKey(a => new { a.PlanId, a.CourseId });

        builder.HasOne(a => a.Plan)
            .WithMany()
            .HasForeignKey(a => a.PlanId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(a => a.Course)
            .WithMany()
            .HasForeignKey(a => a.CourseId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
