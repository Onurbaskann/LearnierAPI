using Learnier.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Learnier.Infrastructure.Persistence.Configurations;

internal sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("roles");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Code).HasMaxLength(64).IsRequired();
        builder.Property(r => r.Name).HasMaxLength(128).IsRequired();

        // Kaynak dokumanin 3. bolumu: (organization_id, code) benzersiz.
        // PostgreSQL'de NULL degerler birbirine esit sayilmadigi icin bu index
        // tek basina sistem rollerinde (organization_id NULL) benzersizligi saglamaz.
        builder.HasIndex(r => new { r.OrganizationId, r.Code }).IsUnique();

        // Sistem rollerinin kodu icin ayri kismi benzersiz index gerekiyor.
        builder.HasIndex(r => r.Code)
            .IsUnique()
            .HasFilter("organization_id IS NULL")
            .HasDatabaseName("ix_roles_code_system_unique");

        builder.HasOne<Organization>()
            .WithMany()
            .HasForeignKey(r => r.OrganizationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(r => r.Permissions)
            .WithOne(p => p.Role)
            .HasForeignKey(p => p.RoleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Metadata
            .FindNavigation(nameof(Role.Permissions))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        // Sistem rolu ile organizasyon rolu birbirini disliyor.
        builder.ToTable(t => t.HasCheckConstraint(
            "ck_roles_system_has_no_organization",
            @"(is_system AND organization_id IS NULL) OR (NOT is_system AND organization_id IS NOT NULL)"));
    }
}
