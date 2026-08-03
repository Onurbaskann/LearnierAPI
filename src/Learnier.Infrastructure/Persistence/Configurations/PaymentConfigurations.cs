using Learnier.Domain.Billing;
using Learnier.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Learnier.Infrastructure.Persistence.Configurations;

internal sealed class PaymentCustomerConfiguration : IEntityTypeConfiguration<PaymentCustomer>
{
    public void Configure(EntityTypeBuilder<PaymentCustomer> builder)
    {
        builder.ToTable("payment_customers");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Provider).HasMaxLength(32).IsRequired();
        builder.Property(c => c.ProviderCustomerId).HasMaxLength(200).IsRequired();

        builder.HasIndex(c => new { c.Provider, c.ProviderCustomerId }).IsUnique();

        builder.HasOne(c => c.User)
            .WithMany()
            .HasForeignKey(c => c.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Organization>()
            .WithMany()
            .HasForeignKey(c => c.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.ToTable(t => t.HasCheckConstraint(
            "ck_payment_customers_single_owner",
            @"(user_id IS NULL) <> (organization_id IS NULL)"));
    }
}

internal sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("payments");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Amount).HasPrecision(18, 2).IsRequired();
        builder.Property(p => p.Currency).HasMaxLength(3).IsRequired();
        builder.Property(p => p.PaymentProvider).HasMaxLength(32).IsRequired();
        builder.Property(p => p.ProviderPaymentId).HasMaxLength(200);
        builder.Property(p => p.FailureReason).HasMaxLength(500);

        builder.Property(p => p.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        // Saglayici webhook'u odemeyi bu kimlikle bulur; ayni bildirimin iki kez
        // islenmesini de bu benzersizlik engeller.
        builder.HasIndex(p => new { p.PaymentProvider, p.ProviderPaymentId })
            .IsUnique()
            .HasFilter(@"provider_payment_id IS NOT NULL");

        builder.HasIndex(p => new { p.SubscriptionId, p.Status });

        builder.HasOne(p => p.Subscription)
            .WithMany()
            .HasForeignKey(p => p.SubscriptionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(p => p.Payer)
            .WithMany()
            .HasForeignKey(p => p.PayerUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(p => p.Refunds)
            .WithOne(r => r.Payment)
            .HasForeignKey(r => r.PaymentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Metadata
            .FindNavigation(nameof(Payment.Refunds))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.ToTable(t => t.HasCheckConstraint(
            "ck_payments_amount_positive",
            @"amount > 0"));

        builder.Ignore(p => p.DomainEvents);
    }
}

internal sealed class RefundConfiguration : IEntityTypeConfiguration<Refund>
{
    public void Configure(EntityTypeBuilder<Refund> builder)
    {
        builder.ToTable("refunds");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Amount).HasPrecision(18, 2).IsRequired();
        builder.Property(r => r.Reason).HasMaxLength(500);
        builder.Property(r => r.ProviderRefundId).HasMaxLength(200);

        builder.Property(r => r.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.HasIndex(r => r.PaymentId);

        builder.ToTable(t => t.HasCheckConstraint(
            "ck_refunds_amount_positive",
            @"amount > 0"));
    }
}
