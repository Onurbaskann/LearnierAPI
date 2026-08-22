using Learnier.Domain.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Learnier.Infrastructure.Persistence.Configurations;

internal sealed class CourseLessonConfiguration : IEntityTypeConfiguration<CourseLesson>
{
    public void Configure(EntityTypeBuilder<CourseLesson> builder)
    {
        builder.ToTable("course_lessons");

        builder.HasKey(l => l.Id);

        builder.Property(l => l.Title).HasMaxLength(200).IsRequired();
        builder.Property(l => l.Description).HasMaxLength(4000);

        builder.HasIndex(l => new { l.ModuleId, l.SortOrder });

        builder.ToTable(t => t.HasCheckConstraint(
            "ck_course_lessons_duration_positive",
            @"estimated_duration_minutes > 0"));
    }
}
