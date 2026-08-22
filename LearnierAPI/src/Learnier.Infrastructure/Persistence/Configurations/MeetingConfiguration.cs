using Learnier.Domain.Scheduling;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Learnier.Infrastructure.Persistence.Configurations;

internal sealed class MeetingConfiguration : IEntityTypeConfiguration<Meeting>
{
    public void Configure(EntityTypeBuilder<Meeting> builder)
    {
        builder.ToTable("meetings");
        builder.HasKey(m => m.Id);

        builder.Property(m => m.Provider).HasMaxLength(32).IsRequired();
        builder.Property(m => m.ProviderMeetingId).HasMaxLength(200);
        builder.Property(m => m.JoinUrl).HasMaxLength(2000);
        builder.Property(m => m.HostUrl).HasMaxLength(2000);
        builder.Property(m => m.LastError).HasMaxLength(2000);
        builder.Property(m => m.Status).HasConversion<string>().HasMaxLength(32).IsRequired();

        builder.HasIndex(m => m.SessionId).IsUnique();
        builder.HasIndex(m => new { m.Provider, m.ProviderMeetingId })
            .IsUnique()
            .HasFilter("provider_meeting_id IS NOT NULL");
        builder.HasIndex(m => new { m.Status, m.CreatedAt });

        builder.HasOne(m => m.Session)
            .WithOne(s => s.Meeting)
            .HasForeignKey<Meeting>(m => m.SessionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.ToTable(t => t.HasCheckConstraint(
            "ck_meetings_time_range",
            "ends_at > starts_at"));

        builder.Ignore(m => m.DomainEvents);
    }
}
