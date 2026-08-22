using Learnier.Domain.Billing;
using Learnier.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Learnier.Infrastructure.Persistence.Configurations;

internal sealed class CancellationPolicyConfiguration
    : IEntityTypeConfiguration<CancellationPolicy>
{
    public void Configure(EntityTypeBuilder<CancellationPolicy> builder)
    {
        builder.ToTable("cancellation_policies");
        builder.HasKey(policy => policy.Id);
        builder.HasIndex(policy => policy.OrganizationId).IsUnique();
        builder.HasOne<Organization>().WithMany()
            .HasForeignKey(policy => policy.OrganizationId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.ToTable(table =>
        {
            table.HasCheckConstraint(
                "ck_cancellation_policy_student_cutoff",
                "student_refund_cutoff_minutes BETWEEN 0 AND 10080");
            table.HasCheckConstraint(
                "ck_cancellation_policy_instructor_cutoff",
                "instructor_penalty_cutoff_minutes BETWEEN 0 AND 10080");
            table.HasCheckConstraint("ck_cancellation_policy_version", "version > 0");
        });
    }
}
