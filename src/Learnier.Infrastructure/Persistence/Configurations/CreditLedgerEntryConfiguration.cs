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

        // Ayni rezervasyon icin Reserve/Consume/Refund hareketi en fazla bir kez
        // yazilir. BookingId bos olan grant/adjust/expire satirlari etkilenmez.
        builder.HasIndex(e => new { e.BookingId, e.TransactionType }).IsUnique();

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

        // Consume, Reserve ile dusmus hakkin tamamlandigini belirten sifir miktarli
        // denetim olayidir. Diger hareketlerin bakiyeye etkisi olmak zorundadir.
        builder.ToTable(t => t.HasCheckConstraint(
            "ck_credit_ledger_quantity_not_zero",
            @"quantity <> 0 OR transaction_type = 'Consume'"));
    }
}
