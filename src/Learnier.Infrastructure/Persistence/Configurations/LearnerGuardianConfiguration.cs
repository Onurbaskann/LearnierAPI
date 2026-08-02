using Learnier.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Learnier.Infrastructure.Persistence.Configurations;

internal sealed class LearnerGuardianConfiguration : IEntityTypeConfiguration<LearnerGuardian>
{
    public void Configure(EntityTypeBuilder<LearnerGuardian> builder)
    {
        builder.ToTable("learner_guardians");

        builder.HasKey(g => g.Id);

        builder.Property(g => g.RelationshipType)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.HasIndex(g => new { g.LearnerUserId, g.GuardianUserId }).IsUnique();
        builder.HasIndex(g => g.GuardianUserId);

        builder.HasOne(g => g.Learner)
            .WithMany()
            .HasForeignKey(g => g.LearnerUserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(g => g.Guardian)
            .WithMany()
            .HasForeignKey(g => g.GuardianUserId)
            // Iki FK de ayni tabloya isaret ettigi icin ikisinde birden Cascade
            // PostgreSQL'de coklu cascade yolu olustururdu; veli tarafi kisitlanir.
            .OnDelete(DeleteBehavior.Restrict);

        builder.ToTable(t => t.HasCheckConstraint(
            "ck_learner_guardians_distinct_users",
            @"learner_user_id <> guardian_user_id"));
    }
}
