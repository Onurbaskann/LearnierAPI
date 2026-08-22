using Learnier.Domain.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Learnier.Infrastructure.Persistence.Configurations;

internal sealed class CourseModuleConfiguration : IEntityTypeConfiguration<CourseModule>
{
    public void Configure(EntityTypeBuilder<CourseModule> builder)
    {
        builder.ToTable("course_modules");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.Title).HasMaxLength(200).IsRequired();
        builder.Property(m => m.Description).HasMaxLength(4000);

        // Sira benzersiz kilinmaz: toplu yeniden siralamada gecici cakismalar
        // dogal olarak olusur ve islemi gereksiz yere kirardi.
        builder.HasIndex(m => new { m.CourseId, m.SortOrder });

        builder.HasMany(m => m.Lessons)
            .WithOne(l => l.Module)
            .HasForeignKey(l => l.ModuleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Metadata
            .FindNavigation(nameof(CourseModule.Lessons))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}
