using Learnier.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Learnier.Infrastructure.Persistence.Configurations;

internal sealed class OrganizationConfiguration : IEntityTypeConfiguration<Organization>
{
    public void Configure(EntityTypeBuilder<Organization> builder)
    {
        builder.ToTable("organizations");

        builder.HasKey(o => o.Id);

        builder.Property(o => o.Name).HasMaxLength(200).IsRequired();

        builder.Property(o => o.Slug).HasMaxLength(100).IsRequired();
        builder.HasIndex(o => o.Slug).IsUnique();

        builder.Property(o => o.OrganizationType)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(o => o.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(o => o.TimeZoneId).HasMaxLength(64).IsRequired();
        builder.Property(o => o.DefaultCurrency).HasMaxLength(3).IsRequired();

        // Kurum-sube iliskisi. Restrict: alt kurumlari olan bir kurum silinemez,
        // once subelerin tasinmasi veya silinmesi gerekir.
        builder.HasOne<Organization>()
            .WithMany()
            .HasForeignKey(o => o.ParentOrganizationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(o => o.Memberships)
            .WithOne(m => m.Organization)
            .HasForeignKey(m => m.OrganizationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Metadata
            .FindNavigation(nameof(Organization.Memberships))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        // Kendi kendine ust kurum olamaz.
        builder.ToTable(t => t.HasCheckConstraint(
            "ck_organizations_parent_not_self",
            @"parent_organization_id IS NULL OR parent_organization_id <> id"));

        builder.Ignore(o => o.DomainEvents);
    }
}
