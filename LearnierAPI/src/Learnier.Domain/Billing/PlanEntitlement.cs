using Learnier.Domain.Common;
using Learnier.Domain.Scheduling;

namespace Learnier.Domain.Billing;

/// <summary>
/// Planin abonesine verdigi hak.
/// </summary>
/// <remarks>
/// Ornekler:
/// <list type="bullet">
///   <item><c>BookingAccess</c> / <c>Group</c> / sinirsiz - sinirsiz grup dersi erisimi.</item>
///   <item><c>LessonCredit</c> / <c>Private</c> / 3 / <c>Week</c> - haftada 3 birebir ders.</item>
/// </list>
/// </remarks>
public sealed class PlanEntitlement : Entity, IAuditableEntity
{
    private PlanEntitlement()
    {
    }

    public Guid PlanId { get; private set; }

    public EntitlementType EntitlementType { get; private set; }

    public SessionType SessionType { get; private set; }

    /// <summary>
    /// Periyot basina verilen adet. Bos ise sinirsiz demektir.
    /// </summary>
    /// <remarks>
    /// Sinirsizlik uygulama katmaninda acikca ele alinmali; <c>null</c> degeri
    /// sessizce sifir gibi davranirsa kullanici hicbir ders alamaz.
    /// </remarks>
    public int? Quantity { get; private set; }

    public EntitlementResetPeriod ResetPeriod { get; private set; }

    /// <summary>
    /// Kredinin karsiladigi birebir ders suresi: 30 veya 50 dakika.
    /// </summary>
    /// <remarks>
    /// Yalnizca <c>LessonCredit</c> + <c>Private</c> hakkinda doludur. Rezervasyon
    /// yetkilendirmesi uygun paketi bu alanla secer; grup ve webinar oturumlari
    /// sure kirilimiyla satilmadigi icin onlarda <c>null</c>'dir.
    /// </remarks>
    public int? LessonDurationMinutes { get; private set; }

    public SubscriptionPlan Plan { get; private set; } = null!;

    public DateTimeOffset CreatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    public Guid? UpdatedBy { get; set; }

    /// <summary>Sinirsiz hak mi.</summary>
    public bool IsUnlimited => Quantity is null;

    internal static PlanEntitlement Create(
        Guid planId,
        EntitlementType entitlementType,
        SessionType sessionType,
        int? quantity,
        EntitlementResetPeriod resetPeriod,
        int? lessonDurationMinutes)
    {
        if (quantity is not null)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity.Value);
        }

        if (entitlementType is EntitlementType.LessonCredit && quantity is null)
        {
            throw new ArgumentException(
                "Ders kredisi hakki icin adet zorunludur; sinirsiz erisim BookingAccess ile tanimlanir.",
                nameof(quantity));
        }

        var isPrivateCredit = entitlementType is EntitlementType.LessonCredit
            && sessionType is SessionType.Private;

        if (isPrivateCredit)
        {
            if (lessonDurationMinutes is null)
            {
                throw new ArgumentException(
                    "Birebir ders kredisi icin ders suresi zorunludur; suresi olmayan kredi "
                    + "hicbir oturumla eslesmez.",
                    nameof(lessonDurationMinutes));
            }

            if (lessonDurationMinutes is not (30 or 50))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(lessonDurationMinutes),
                    lessonDurationMinutes,
                    "Ders suresi yalnizca 30 veya 50 dakika olabilir.");
            }
        }
        else if (lessonDurationMinutes is not null)
        {
            throw new ArgumentException(
                "Ders suresi yalnizca birebir ders kredisinde anlamlidir.",
                nameof(lessonDurationMinutes));
        }

        return new PlanEntitlement
        {
            PlanId = planId,
            EntitlementType = entitlementType,
            SessionType = sessionType,
            Quantity = quantity,
            ResetPeriod = resetPeriod,
            LessonDurationMinutes = lessonDurationMinutes
        };
    }
}
