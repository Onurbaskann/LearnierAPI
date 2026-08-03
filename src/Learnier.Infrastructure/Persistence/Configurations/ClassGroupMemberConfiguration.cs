using Learnier.Domain.Scheduling;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Learnier.Infrastructure.Persistence.Configurations;

internal sealed class ClassGroupMemberConfiguration : IEntityTypeConfiguration<ClassGroupMember>
{
    public void Configure(EntityTypeBuilder<ClassGroupMember> builder)
    {
        builder.ToTable("class_group_members");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        // Kaynak dokumanin 13. bolumundeki zorunlu benzersizlik.
        builder.HasIndex(m => new { m.ClassGroupId, m.LearnerUserId }).IsUnique();

        // "Ogrencinin siniflari" sorgusu.
        builder.HasIndex(m => new { m.LearnerUserId, m.Status });

        builder.HasOne(m => m.Learner)
            .WithMany()
            .HasForeignKey(m => m.LearnerUserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.ToTable(t => t.HasCheckConstraint(
            "ck_class_group_members_left_after_enrolled",
            @"left_at IS NULL OR left_at >= enrolled_at"));
    }
}
