using Learnier.Domain.Identity;
using Learnier.Domain.Scheduling;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Learnier.Infrastructure.Persistence.Configurations;

internal sealed class ClassGroupConfiguration : IEntityTypeConfiguration<ClassGroup>
{
    public void Configure(EntityTypeBuilder<ClassGroup> builder)
    {
        builder.ToTable("class_groups");

        builder.HasKey(g => g.Id);

        builder.Property(g => g.Name).HasMaxLength(200).IsRequired();

        builder.Property(g => g.DeliveryType)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(g => g.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.HasIndex(g => new { g.OrganizationId, g.Status });
        builder.HasIndex(g => g.CourseId);

        builder.HasOne<Organization>()
            .WithMany()
            .HasForeignKey(g => g.OrganizationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(g => g.Course)
            .WithMany()
            .HasForeignKey(g => g.CourseId)
            // Uzerinde sinif olan egitim silinemez; once sinif kapatilmali.
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(g => g.Members)
            .WithOne(m => m.ClassGroup)
            .HasForeignKey(m => m.ClassGroupId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Metadata
            .FindNavigation(nameof(ClassGroup.Members))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.ToTable(t =>
        {
            t.HasCheckConstraint("ck_class_groups_capacity_positive", @"capacity > 0");

            t.HasCheckConstraint(
                "ck_class_groups_date_range",
                @"starts_on IS NULL OR ends_on IS NULL OR ends_on >= starts_on");
        });

        builder.Ignore(g => g.DomainEvents);
    }
}
