using Learnier.Domain.Billing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Learnier.Infrastructure.Persistence.Configurations;

internal sealed class PlanPriceConfiguration : IEntityTypeConfiguration<PlanPrice>
{
    public void Configure(EntityTypeBuilder<PlanPrice> builder)
    {
        builder.ToTable("plan_prices");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Currency).HasMaxLength(3).IsRequired();
        builder.Property(p => p.Amount).HasPrecision(18, 2).IsRequired();

        builder.Property(p => p.BillingInterval)
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(p => p.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        // Satis ekrani "su planin su para birimindeki gecerli fiyati" sorgusunu yapar.
        builder.HasIndex(p => new { p.PlanId, p.Currency, p.Status });

        builder.ToTable(t =>
        {
            t.HasCheckConstraint("ck_plan_prices_amount_not_negative", @"amount >= 0");

            t.HasCheckConstraint(
                "ck_plan_prices_interval_count_positive",
                @"billing_interval_count > 0");

            t.HasCheckConstraint(
                "ck_plan_prices_valid_range",
                @"valid_until IS NULL OR valid_until >= valid_from");
        });
    }
}
