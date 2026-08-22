using Learnier.Domain.Billing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Learnier.Infrastructure.Persistence.Configurations;

internal sealed class PlanEntitlementConfiguration : IEntityTypeConfiguration<PlanEntitlement>
{
    public void Configure(EntityTypeBuilder<PlanEntitlement> builder)
    {
        builder.ToTable("plan_entitlements");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.EntitlementType)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(e => e.SessionType)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(e => e.ResetPeriod)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(e => e.LessonDurationMinutes);

        // Ayni plan bir ders turu icin tek hak tanimi icerir. Birebir ders kredisi
        // sure kirilimindadir: 30 ve 50 dakika ayri haklardir.
        builder.HasIndex(e => new
        {
            e.PlanId,
            e.EntitlementType,
            e.SessionType,
            e.LessonDurationMinutes
        }).IsUnique();

        builder.ToTable(t =>
        {
            t.HasCheckConstraint(
                "ck_plan_entitlements_quantity_positive",
                @"quantity IS NULL OR quantity > 0");

            // Sinirsizlik yalnizca erisim hakkinda anlamlidir; sayili ders
            // hakkinda adet bos birakilirsa hak hesaplanamaz hale gelir.
            t.HasCheckConstraint(
                "ck_plan_entitlements_credit_requires_quantity",
                @"entitlement_type <> 'LessonCredit' OR quantity IS NOT NULL");

            // Rezervasyon yetkilendirmesi uygun paketi ders suresiyle secer:
            // suresiz birebir kredi hicbir oturumla eslesmez, sureli grup hakki
            // ise hicbir yerde okunmaz.
            t.HasCheckConstraint(
                "ck_plan_entitlements_private_credit_duration",
                "(entitlement_type = 'LessonCredit' AND session_type = 'Private') "
                + "= (lesson_duration_minutes IS NOT NULL)");

            t.HasCheckConstraint(
                "ck_plan_entitlements_lesson_duration",
                "lesson_duration_minutes IS NULL OR lesson_duration_minutes IN (30, 50)");
        });
    }
}
