using Learnier.Domain.Identity;
using Learnier.Domain.Scheduling;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Learnier.Infrastructure.Persistence.Configurations;

internal sealed class SessionAttendanceConfiguration : IEntityTypeConfiguration<SessionAttendance>
{
    public void Configure(EntityTypeBuilder<SessionAttendance> builder)
    {
        builder.ToTable("session_attendances");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        // Rezervasyon basina tek katilim kaydi.
        builder.HasIndex(a => a.BookingId).IsUnique();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(a => a.MarkedByUserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.ToTable(t =>
        {
            t.HasCheckConstraint(
                "ck_session_attendances_minutes_not_negative",
                @"attended_minutes >= 0");

            t.HasCheckConstraint(
                "ck_session_attendances_time_range",
                @"joined_at IS NULL OR left_at IS NULL OR left_at >= joined_at");
        });
    }
}
