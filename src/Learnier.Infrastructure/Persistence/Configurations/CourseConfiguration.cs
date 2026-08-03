using Learnier.Domain.Catalog;
using Learnier.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Learnier.Infrastructure.Persistence.Configurations;

internal sealed class CourseConfiguration : IEntityTypeConfiguration<Course>
{
    public void Configure(EntityTypeBuilder<Course> builder)
    {
        builder.ToTable("courses");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Title).HasMaxLength(200).IsRequired();
        builder.Property(c => c.Description).HasMaxLength(4000);

        builder.Property(c => c.CourseType)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(c => c.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        // Katalog listeleri kurum + alan + durum uzerinden filtrelenir.
        builder.HasIndex(c => new { c.OrganizationId, c.SubjectId, c.Status });

        builder.HasOne<Organization>()
            .WithMany()
            .HasForeignKey(c => c.OrganizationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(c => c.Subject)
            .WithMany()
            .HasForeignKey(c => c.SubjectId)
            // Uzerinde egitim olan bir alan silinemez.
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Level>()
            .WithMany()
            .HasForeignKey(c => c.LevelId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(c => c.Modules)
            .WithOne(m => m.Course)
            .HasForeignKey(m => m.CourseId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Metadata
            .FindNavigation(nameof(Course.Modules))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.ToTable(t =>
        {
            t.HasCheckConstraint(
                "ck_courses_duration_positive",
                @"default_duration_minutes > 0");

            t.HasCheckConstraint(
                "ck_courses_participant_range",
                @"min_participants >= 1 AND max_participants >= min_participants");
        });

        builder.Ignore(c => c.DomainEvents);
    }
}
