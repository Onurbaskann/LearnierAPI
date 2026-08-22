using Learnier.Domain.Billing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Learnier.Infrastructure.Persistence.Configurations;

internal sealed class SubscriptionSeatConfiguration : IEntityTypeConfiguration<SubscriptionSeat>
{
    public void Configure(EntityTypeBuilder<SubscriptionSeat> builder)
    {
        builder.ToTable("subscription_seats");

        builder.HasKey(s => s.Id);

        // Benzersizlik yalnizca aktif koltuklar icin gecerlidir. Kosulsuz bir
        // unique index, koltugu geri alinip yeniden verilen calisani engellerdi.
        builder.HasIndex(s => new { s.SubscriptionId, s.MembershipId })
            .IsUnique()
            .HasFilter(@"revoked_at IS NULL");

        builder.HasIndex(s => s.MembershipId);

        builder.HasOne(s => s.Membership)
            .WithMany()
            .HasForeignKey(s => s.MembershipId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.ToTable(t => t.HasCheckConstraint(
            "ck_subscription_seats_revoked_after_assigned",
            @"revoked_at IS NULL OR revoked_at >= assigned_at"));
    }
}
