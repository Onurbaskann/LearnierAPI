using Learnier.Domain.Billing;
using Learnier.Domain.Scheduling;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Learnier.Infrastructure.Persistence.Configurations;

internal sealed class CreditLedgerEntryConfiguration : IEntityTypeConfiguration<CreditLedgerEntry>
{
    public void Configure(EntityTypeBuilder<CreditLedgerEntry> builder)
    {
        builder.ToTable("credit_ledger");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.SessionType)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(e => e.TransactionType)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        // Kalan hak sorgusu: abonelik + ogrenci + son kullanma.
        builder.HasIndex(e => new { e.SubscriptionId, e.LearnerUserId, e.ExpiresAt });

        // Ders turune gore bakiye hesabi.
        builder.HasIndex(e => new { e.LearnerUserId, e.SessionType });

        builder.HasOne(e => e.Subscription)
            .WithMany()
            .HasForeignKey(e => e.SubscriptionId)
            // Defter silinmez: aboneligin gecmisi silinse bile hareketler
            // muhasebe ve destek talepleri icin gereklidir.
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Learner)
            .WithMany()
            .HasForeignKey(e => e.LearnerUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<SessionBooking>()
            .WithMany()
            .HasForeignKey(e => e.BookingId)
            .OnDelete(DeleteBehavior.SetNull);

        // Sifir miktarli hareket defterde anlamsizdir ve bakiyeyi degistirmez.
        builder.ToTable(t => t.HasCheckConstraint(
            "ck_credit_ledger_quantity_not_zero",
            @"quantity <> 0"));
    }
}
