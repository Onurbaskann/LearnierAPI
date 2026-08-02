using Learnier.Domain.Common;

namespace Learnier.Domain.Identity;

/// <summary>
/// Bir kullanicinin bir organizasyondaki uyeligi.
/// </summary>
/// <remarks>
/// Sistemdeki yetkilendirmenin merkezi burasidir. Roller kullaniciya degil uyelige
/// baglanir; boylece ayni kisi A kurumunda egitmen, B kurumunda ogrenci olabilir.
/// Egitmen profili gibi kayitlar da kullaniciya degil uyelige baglanir.
/// </remarks>
public sealed class OrganizationMembership : Entity, IAuditableEntity, ITenantScoped
{
    private readonly List<MembershipRole> _roles = [];

    private OrganizationMembership()
    {
    }

    public Guid OrganizationId { get; private set; }

    public Guid UserId { get; private set; }

    public MembershipStatus Status { get; private set; }

    public DateTimeOffset? JoinedAt { get; private set; }

    public Organization Organization { get; private set; } = null!;

    public User User { get; private set; } = null!;

    public IReadOnlyCollection<MembershipRole> Roles => _roles.AsReadOnly();

    public DateTimeOffset CreatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    public Guid? UpdatedBy { get; set; }

    internal static OrganizationMembership Create(
        Guid organizationId,
        Guid userId,
        MembershipStatus status,
        DateTimeOffset? joinedAt)
        => new()
        {
            OrganizationId = organizationId,
            UserId = userId,
            Status = status,
            JoinedAt = joinedAt
        };

    public void Accept(DateTimeOffset acceptedAt)
    {
        if (Status is not MembershipStatus.Invited)
        {
            return;
        }

        Status = MembershipStatus.Active;
        JoinedAt = acceptedAt;
    }

    public void Suspend() => Status = MembershipStatus.Suspended;

    /// <summary>
    /// Uyelige rol atar. Ayni rol iki kez atanmaz.
    /// </summary>
    public void AssignRole(Guid roleId)
    {
        if (_roles.Exists(r => r.RoleId == roleId))
        {
            return;
        }

        _roles.Add(MembershipRole.Create(Id, roleId));
    }

    public void RemoveRole(Guid roleId) => _roles.RemoveAll(r => r.RoleId == roleId);
}
