using Learnier.Domain.Billing;
using Learnier.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Learnier.Infrastructure.Persistence.Configurations;

internal sealed class CheckoutSessionConfiguration : IEntityTypeConfiguration<CheckoutSession>
{
    public void Configure(EntityTypeBuilder<CheckoutSession> builder)
    {
        builder.ToTable("checkout_sessions");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Provider).HasMaxLength(32).IsRequired();
        builder.Property(c => c.IdempotencyKey).HasMaxLength(200).IsRequired();
        builder.Property(c => c.ProviderCheckoutSessionId).HasMaxLength(200);
        builder.Property(c => c.CheckoutUrl).HasMaxLength(2000);
        builder.Property(c => c.Amount).HasPrecision(18, 2).IsRequired();
        builder.Property(c => c.Currency).HasMaxLength(3).IsRequired();
        builder.Property(c => c.Status).HasConversion<string>().HasMaxLength(32).IsRequired();

        builder.HasIndex(c => new { c.Provider, c.IdempotencyKey }).IsUnique();
        builder.HasIndex(c => new { c.Provider, c.ProviderCheckoutSessionId })
            .IsUnique()
            .HasFilter("provider_checkout_session_id IS NOT NULL");
        builder.HasIndex(c => new { c.UserId, c.Status });
        builder.HasIndex(c => c.PaymentId).IsUnique().HasFilter("payment_id IS NOT NULL");

        builder.HasOne(c => c.User)
            .WithMany()
            .HasForeignKey(c => c.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.PlanPrice)
            .WithMany()
            .HasForeignKey(c => c.PlanPriceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.Payment)
            .WithOne()
            .HasForeignKey<CheckoutSession>(c => c.PaymentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.ToTable(t => t.HasCheckConstraint("ck_checkout_sessions_amount_positive", "amount > 0"));
        builder.Ignore(c => c.DomainEvents);
    }
}

internal sealed class PaymentAttemptConfiguration : IEntityTypeConfiguration<PaymentAttempt>
{
    public void Configure(EntityTypeBuilder<PaymentAttempt> builder)
    {
        builder.ToTable("payment_attempts");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Provider).HasMaxLength(32).IsRequired();
        builder.Property(a => a.IdempotencyKey).HasMaxLength(200).IsRequired();
        builder.Property(a => a.ProviderPaymentAttemptId).HasMaxLength(200);
        builder.Property(a => a.Amount).HasPrecision(18, 2).IsRequired();
        builder.Property(a => a.Currency).HasMaxLength(3).IsRequired();
        builder.Property(a => a.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(a => a.NextActionUrl).HasMaxLength(2000);
        builder.Property(a => a.FailureCode).HasMaxLength(100);
        builder.Property(a => a.FailureMessage).HasMaxLength(1000);

        builder.HasIndex(a => new { a.Provider, a.IdempotencyKey }).IsUnique();
        builder.HasIndex(a => new { a.Provider, a.ProviderPaymentAttemptId })
            .IsUnique()
            .HasFilter("provider_payment_attempt_id IS NOT NULL");
        builder.HasIndex(a => new { a.CheckoutSessionId, a.Status });

        builder.HasOne(a => a.CheckoutSession)
            .WithMany()
            .HasForeignKey(a => a.CheckoutSessionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(a => a.Payment)
            .WithMany()
            .HasForeignKey(a => a.PaymentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.ToTable(t => t.HasCheckConstraint("ck_payment_attempts_amount_positive", "amount > 0"));
    }
}

internal sealed class PaymentWebhookInboxConfiguration
    : IEntityTypeConfiguration<PaymentWebhookInbox>
{
    public void Configure(EntityTypeBuilder<PaymentWebhookInbox> builder)
    {
        builder.ToTable("payment_webhook_inbox");
        builder.HasKey(w => w.Id);

        builder.Property(w => w.Provider).HasMaxLength(32).IsRequired();
        builder.Property(w => w.ProviderEventId).HasMaxLength(200).IsRequired();
        builder.Property(w => w.EventType).HasMaxLength(200).IsRequired();
        builder.Property(w => w.Payload).HasColumnType("jsonb").IsRequired();
        builder.Property(w => w.PayloadSha256).HasMaxLength(64).IsRequired();
        builder.Property(w => w.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(w => w.LastError).HasMaxLength(2000);

        builder.HasIndex(w => new { w.Provider, w.ProviderEventId }).IsUnique();
        builder.HasIndex(w => new { w.Status, w.ReceivedAt });
    }
}

internal sealed class RefundRequestConfiguration : IEntityTypeConfiguration<RefundRequest>
{
    public void Configure(EntityTypeBuilder<RefundRequest> builder)
    {
        builder.ToTable("refund_requests");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Amount).HasPrecision(18, 2).IsRequired();
        builder.Property(r => r.Provider).HasMaxLength(32).IsRequired();
        builder.Property(r => r.IdempotencyKey).HasMaxLength(200).IsRequired();
        builder.Property(r => r.Reason).HasMaxLength(500);
        builder.Property(r => r.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(r => r.FailureCode).HasMaxLength(100);
        builder.Property(r => r.FailureMessage).HasMaxLength(1000);

        builder.HasIndex(r => r.RefundId).IsUnique();
        builder.HasIndex(r => new { r.Provider, r.IdempotencyKey }).IsUnique();
        builder.HasIndex(r => new { r.Status, r.CreatedAt });

        builder.HasOne(r => r.Payment)
            .WithMany()
            .HasForeignKey(r => r.PaymentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.Refund)
            .WithOne()
            .HasForeignKey<RefundRequest>(r => r.RefundId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.RequestedByUser)
            .WithMany()
            .HasForeignKey(r => r.RequestedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.ToTable(t => t.HasCheckConstraint("ck_refund_requests_amount_positive", "amount > 0"));
    }
}
