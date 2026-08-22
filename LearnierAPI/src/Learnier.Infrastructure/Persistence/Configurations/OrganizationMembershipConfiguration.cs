using Learnier.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Learnier.Infrastructure.Persistence.Configurations;

internal sealed class OrganizationMembershipConfiguration
    : IEntityTypeConfiguration<OrganizationMembership>
{
    public void Configure(EntityTypeBuilder<OrganizationMembership> builder)
    {
        builder.ToTable("organization_memberships");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        // Kaynak dokumanin 13. bolumu: bir kullanici bir organizasyonda
        // yalnizca bir kez uye olabilir.
        builder.HasIndex(m => new { m.OrganizationId, m.UserId }).IsUnique();

        // Tenant cozumlemesi bu sorguyu her istekte calistirir.
        builder.HasIndex(m => new { m.UserId, m.Status });

        builder.HasMany(m => m.Roles)
            .WithOne(r => r.Membership)
            .HasForeignKey(r => r.MembershipId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Metadata
            .FindNavigation(nameof(OrganizationMembership.Roles))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}
