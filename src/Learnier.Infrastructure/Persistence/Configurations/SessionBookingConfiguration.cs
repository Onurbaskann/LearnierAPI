using Learnier.Domain.Billing;
using Learnier.Domain.Identity;
using Learnier.Domain.Scheduling;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Learnier.Infrastructure.Persistence.Configurations;

internal sealed class SessionBookingConfiguration : IEntityTypeConfiguration<SessionBooking>
{
    public void Configure(EntityTypeBuilder<SessionBooking> builder)
    {
        builder.ToTable("session_bookings");

        builder.HasKey(b => b.Id);

        builder.Property(b => b.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(b => b.AccessSource)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(b => b.CancellationReason).HasMaxLength(500);

        // Ayni ogrenci ayni oturuma iki kez rezervasyon yapamaz.
        // Iptal edip yeniden rezervasyon yapabilmesi icin mevcut satir
        // yeniden kullanilir; yeni satir acilmaz.
        builder.HasIndex(b => new { b.SessionId, b.LearnerUserId }).IsUnique();

        // Kontenjan sayimi ve bekleme listesi siralamasi.
        builder.HasIndex(b => new { b.SessionId, b.Status });

        // "Ogrencinin dersleri" sorgusu.
        builder.HasIndex(b => new { b.LearnerUserId, b.Status });

        builder.HasOne(b => b.Learner)
            .WithMany()
            .HasForeignKey(b => b.LearnerUserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(b => b.BookedByUserId)
            // Iki FK de users tablosuna gidiyor; ikisinde birden cascade
            // PostgreSQL'de coklu cascade yolu olustururdu.
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(b => b.Subscription)
            .WithMany()
            .HasForeignKey(b => b.SubscriptionId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(b => b.Attendance)
            .WithOne(a => a.Booking)
            .HasForeignKey<SessionAttendance>(a => a.BookingId)
            .OnDelete(DeleteBehavior.Cascade);

        // Harcanan kredi hareketi. Kayit iki yonlu baglidir: defter hareketi
        // rezervasyonu, rezervasyon da hareketi gosterir. Rezervasyon once
        // eklenip hareket sonra baglandigi icin bu kolon gecici olarak bos kalir;
        // bu yuzden "kredi ile alindiysa hareket dolu olmali" kurali veritabaninda
        // degil, rezervasyon handler'inin islemi icinde korunur.
        builder.HasOne<CreditLedgerEntry>()
            .WithMany()
            .HasForeignKey(b => b.CreditLedgerEntryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.ToTable(t => t.HasCheckConstraint(
            "ck_session_bookings_cancelled_at_present",
            @"(status = 'Cancelled') = (cancelled_at IS NOT NULL)"));
    }
}
