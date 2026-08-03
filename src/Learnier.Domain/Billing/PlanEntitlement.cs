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
        EntitlementResetPeriod resetPeriod)
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

        return new PlanEntitlement
        {
            PlanId = planId,
            EntitlementType = entitlementType,
            SessionType = sessionType,
            Quantity = quantity,
            ResetPeriod = resetPeriod
        };
    }
}
