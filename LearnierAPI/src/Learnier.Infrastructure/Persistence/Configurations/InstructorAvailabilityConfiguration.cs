using Learnier.Domain.Teaching;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Learnier.Infrastructure.Persistence.Configurations;

internal sealed class InstructorAvailabilityConfiguration : IEntityTypeConfiguration<InstructorAvailability>
{
    public void Configure(EntityTypeBuilder<InstructorAvailability> builder)
    {
        builder.ToTable("instructor_availabilities");

        builder.HasKey(a => a.Id);

        // DayOfWeek metin olarak saklanir: enum sirasi degisirse gunler kaymasin.
        builder.Property(a => a.DayOfWeek)
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(a => a.TimeZoneId).HasMaxLength(64).IsRequired();

        // Slot uretimi "su egitmenin su gunku gecerli araliklari" sorgusuyla baslar.
        builder.HasIndex(a => new { a.InstructorProfileId, a.DayOfWeek, a.ValidFrom });

        builder.ToTable(t =>
        {
            t.HasCheckConstraint(
                "ck_instructor_availabilities_time_range",
                @"end_local_time > start_local_time");

            t.HasCheckConstraint(
                "ck_instructor_availabilities_valid_range",
                @"valid_until IS NULL OR valid_until >= valid_from");
        });
    }
}
