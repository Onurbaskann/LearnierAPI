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

        builder.Property(e => e.PeriodStart);

        // Kalan hak sorgusu: abonelik + ogrenci + son kullanma.
        builder.HasIndex(e => new { e.SubscriptionId, e.LearnerUserId, e.ExpiresAt });

        // Worker yalnizca suresi dolan aylik grant'leri tarar. Kismi indeks
        // rezervasyon/iade satirlarini disarida tutarak taramayi kucuk tutar.
        builder.HasIndex(e => new { e.ExpiresAt, e.SubscriptionId })
            .HasDatabaseName("ix_credit_ledger_due_period_grants")
            .HasFilter("transaction_type = 'PeriodGrant'");

        // Ders turune gore bakiye hesabi.
        builder.HasIndex(e => new { e.LearnerUserId, e.SessionType });

        // Ayni rezervasyon icin Reserve/Consume/Refund hareketi en fazla bir kez
        // yazilir. BookingId bos olan grant/adjust/expire satirlari etkilenmez.
        builder.HasIndex(e => new { e.BookingId, e.TransactionType }).IsUnique();

        // Bir aylik donem icin grant ve expire hareketleri yalnizca bir kez
        // yazilabilir. Kismi indeks rezervasyon hareketlerini bu indekse almaz.
        builder.HasIndex(e => new
            {
                e.SubscriptionId,
                e.SessionType,
                e.TransactionType,
                e.PeriodStart
            })
            .IsUnique()
            .HasFilter(
                "period_start IS NOT NULL AND transaction_type IN ('PeriodGrant', 'Expire')");

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
