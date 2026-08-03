using Learnier.Domain.Billing;
using Learnier.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Learnier.Infrastructure.Persistence.Configurations;

internal sealed class SubscriptionConfiguration : IEntityTypeConfiguration<Subscription>
{
    public void Configure(EntityTypeBuilder<Subscription> builder)
    {
        builder.ToTable("subscriptions");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(s => s.PaymentProvider).HasMaxLength(32);
        builder.Property(s => s.ProviderSubscriptionId).HasMaxLength(200);

        builder.HasIndex(s => new { s.SubscriberUserId, s.Status });
        builder.HasIndex(s => new { s.SubscriberOrganizationId, s.Status });

        // Saglayicidan gelen webhook'lar aboneligi bu kimlikle bulur.
        builder.HasIndex(s => new { s.PaymentProvider, s.ProviderSubscriptionId })
            .IsUnique()
            .HasFilter(@"provider_subscription_id IS NOT NULL");

        // Egitimi saglayan kurum (kiraci).
        builder.HasOne<Organization>()
            .WithMany()
            .HasForeignKey(s => s.OrganizationId)
            .OnDelete(DeleteBehavior.Cascade);

        // Abonelik satin alan kurum - kurumsal abonelikte dolu olur.
        builder.HasOne<Organization>()
            .WithMany()
            .HasForeignKey(s => s.SubscriberOrganizationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.SubscriberUser)
            .WithMany()
            .HasForeignKey(s => s.SubscriberUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.PlanPrice)
            .WithMany()
            .HasForeignKey(s => s.PlanPriceId)
            // Fiyat surumu silinemez: aboneligin hangi tutardan satildigi kaybolmamali.
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(s => s.Seats)
            .WithOne(seat => seat.Subscription)
            .HasForeignKey(seat => seat.SubscriptionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Metadata
            .FindNavigation(nameof(Subscription.Seats))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.ToTable(t =>
        {
            // Abone ya kullanici ya kurumdur; ikisi birden veya hicbiri olamaz.
            t.HasCheckConstraint(
                "ck_subscriptions_single_subscriber",
                @"(subscriber_user_id IS NULL) <> (subscriber_organization_id IS NULL)");

            t.HasCheckConstraint(
                "ck_subscriptions_period_range",
                @"current_period_end > current_period_start");
        });

        builder.Ignore(s => s.DomainEvents);
    }
}
