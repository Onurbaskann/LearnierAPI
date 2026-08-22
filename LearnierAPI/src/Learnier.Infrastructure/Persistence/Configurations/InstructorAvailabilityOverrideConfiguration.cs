using Learnier.Domain.Teaching;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Learnier.Infrastructure.Persistence.Configurations;

internal sealed class InstructorAvailabilityOverrideConfiguration
    : IEntityTypeConfiguration<InstructorAvailabilityOverride>
{
    public void Configure(EntityTypeBuilder<InstructorAvailabilityOverride> builder)
    {
        builder.ToTable("instructor_availability_overrides");

        builder.HasKey(o => o.Id);

        builder.Property(o => o.OverrideType)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(o => o.Reason).HasMaxLength(500);

        // Slot uretimi once haftalik araligi, sonra o gune ait istisnalari okur.
        builder.HasIndex(o => new { o.InstructorProfileId, o.OverrideDate });

        builder.HasOne(o => o.InstructorProfile)
            .WithMany()
            .HasForeignKey(o => o.InstructorProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.ToTable(t =>
        {
            // Saatler ya birlikte verilir ya da istisna gun boyunu kapsar.
            t.HasCheckConstraint(
                "ck_availability_overrides_times_paired",
                @"(start_local_time IS NULL) = (end_local_time IS NULL)");

            t.HasCheckConstraint(
                "ck_availability_overrides_time_range",
                @"start_local_time IS NULL OR end_local_time > start_local_time");
        });
    }
}
