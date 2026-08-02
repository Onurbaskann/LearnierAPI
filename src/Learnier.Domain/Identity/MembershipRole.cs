using Learnier.Domain.Common;

namespace Learnier.Domain.Identity;

/// <summary>
/// Uyelik ile rol arasindaki baglanti.
/// </summary>
/// <remarks>
/// Rolun kullaniciya degil uyelige baglanmasi, coklu kurum senaryosunun temelidir.
/// </remarks>
public sealed class MembershipRole : Entity
{
    private MembershipRole()
    {
    }

    public Guid MembershipId { get; private set; }

    public Guid RoleId { get; private set; }

    public OrganizationMembership Membership { get; private set; } = null!;

    public Role Role { get; private set; } = null!;

    internal static MembershipRole Create(Guid membershipId, Guid roleId)
        => new() { MembershipId = membershipId, RoleId = roleId };
}
