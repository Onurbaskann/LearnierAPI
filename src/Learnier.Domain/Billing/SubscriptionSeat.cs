using Learnier.Domain.Common;
using Learnier.Domain.Identity;

namespace Learnier.Domain.Billing;

/// <summary>
/// Kurumsal abonelikte bir uyelige ayrilmis koltuk.
/// </summary>
/// <remarks>
/// Sirket 100 kisilik abonelik alip calisanlarina koltuk atayabilir. Koltuk
/// kullaniciya degil <see cref="OrganizationMembership"/>'e baglanir: calisan
/// isten ayrildiginda uyelik kapanir ve koltuk dogal olarak bosalir.
/// </remarks>
public sealed class SubscriptionSeat : Entity, IAuditableEntity
{
    private SubscriptionSeat()
    {
    }

    public Guid SubscriptionId { get; private set; }

    public Guid MembershipId { get; private set; }

    public DateTimeOffset AssignedAt { get; private set; }

    /// <summary>Bos ise koltuk hala aktiftir.</summary>
    public DateTimeOffset? RevokedAt { get; private set; }

    public Subscription Subscription { get; private set; } = null!;

    public OrganizationMembership Membership { get; private set; } = null!;

    public DateTimeOffset CreatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    public Guid? UpdatedBy { get; set; }

    internal static SubscriptionSeat Create(Guid subscriptionId, Guid membershipId, DateTimeOffset assignedAt)
        => new()
        {
            SubscriptionId = subscriptionId,
            MembershipId = membershipId,
            AssignedAt = assignedAt
        };

    /// <summary>
    /// Koltugu geri alir. Kayit silinmez ki koltugun kimde ne kadar kaldigi izlenebilsin.
    /// </summary>
    public void Revoke(DateTimeOffset revokedAt) => RevokedAt = revokedAt;
}
