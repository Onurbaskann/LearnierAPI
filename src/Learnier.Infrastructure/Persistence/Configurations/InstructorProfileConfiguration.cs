using Learnier.Domain.Teaching;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Learnier.Infrastructure.Persistence.Configurations;

internal sealed class InstructorProfileConfiguration : IEntityTypeConfiguration<InstructorProfile>
{
    public void Configure(EntityTypeBuilder<InstructorProfile> builder)
    {
        builder.ToTable("instructor_profiles");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Bio).HasMaxLength(4000);
        builder.Property(p => p.Headline).HasMaxLength(160);
        builder.Property(p => p.Hobbies).HasMaxLength(500);
        builder.Property(p => p.TimeZoneId).HasMaxLength(64).IsRequired();

        builder.Property(p => p.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        // Para tutarlari icin ondalik kesinlik acikca verilir; aksi halde
        // saglayici varsayilanina birakilir ve yuvarlanma davranisi belirsizlesir.
        builder.Property(p => p.DefaultHourlyRate).HasPrecision(18, 2);
        builder.Property(p => p.DefaultHourlyRateCurrency).HasMaxLength(3);

        // Bir uyelige tek egitmen profili baglanir.
        builder.HasIndex(p => p.MembershipId).IsUnique();

        builder.HasOne(p => p.Membership)
            .WithOne()
            .HasForeignKey<InstructorProfile>(p => p.MembershipId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(p => p.Subjects)
            .WithOne(s => s.InstructorProfile)
            .HasForeignKey(s => s.InstructorProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(p => p.Availabilities)
            .WithOne(a => a.InstructorProfile)
            .HasForeignKey(a => a.InstructorProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Metadata
            .FindNavigation(nameof(InstructorProfile.Subjects))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.Metadata
            .FindNavigation(nameof(InstructorProfile.Availabilities))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.ToTable(t =>
        {
            // Tutar ile para birimi ayrilmaz: biri varsa digeri de olmali.
            t.HasCheckConstraint(
                "ck_instructor_profiles_rate_currency_paired",
                @"(default_hourly_rate IS NULL) = (default_hourly_rate_currency IS NULL)");

            t.HasCheckConstraint(
                "ck_instructor_profiles_rate_not_negative",
                @"default_hourly_rate IS NULL OR default_hourly_rate >= 0");
        });

        builder.Ignore(p => p.DomainEvents);
    }
}
