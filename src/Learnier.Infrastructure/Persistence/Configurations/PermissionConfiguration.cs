using Learnier.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Learnier.Infrastructure.Persistence.Configurations;

internal sealed class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> builder)
    {
        builder.ToTable("permissions");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Code).HasMaxLength(64).IsRequired();
        builder.HasIndex(p => p.Code).IsUnique();
    }
}

internal sealed class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermission>
{
    public void Configure(EntityTypeBuilder<RolePermission> builder)
    {
        builder.ToTable("role_permissions");

        builder.HasKey(rp => rp.Id);

        builder.HasIndex(rp => new { rp.RoleId, rp.PermissionId }).IsUnique();

        builder.HasOne(rp => rp.Permission)
            .WithMany()
            .HasForeignKey(rp => rp.PermissionId)
            // Kullanimda olan bir izin silinemez: once rollerden kaldirilmali.
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class MembershipRoleConfiguration : IEntityTypeConfiguration<MembershipRole>
{
    public void Configure(EntityTypeBuilder<MembershipRole> builder)
    {
        builder.ToTable("membership_roles");

        builder.HasKey(mr => mr.Id);

        builder.HasIndex(mr => new { mr.MembershipId, mr.RoleId }).IsUnique();

        builder.HasOne(mr => mr.Role)
            .WithMany()
            .HasForeignKey(mr => mr.RoleId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
