using Learnier.Domain.Common;
using Learnier.Domain.Identity;

namespace Learnier.Domain.Scheduling;

/// <summary>
/// Ogrencinin bir sinifa kaydi.
/// </summary>
/// <remarks>
/// Kayit silinmez, <see cref="Status"/> ve <see cref="LeftAt"/> ile kapatilir:
/// ayrilan ogrencinin gecmis katilim ve ilerleme kayitlari anlamini korumali.
/// </remarks>
public sealed class ClassGroupMember : Entity, IAuditableEntity
{
    private ClassGroupMember()
    {
    }

    public Guid ClassGroupId { get; private set; }

    public Guid LearnerUserId { get; private set; }

    public ClassGroupMemberStatus Status { get; private set; }

    public DateTimeOffset EnrolledAt { get; private set; }

    public DateTimeOffset? LeftAt { get; private set; }

    public ClassGroup ClassGroup { get; private set; } = null!;

    public User Learner { get; private set; } = null!;

    public DateTimeOffset CreatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    public Guid? UpdatedBy { get; set; }

    internal static ClassGroupMember Create(Guid classGroupId, Guid learnerUserId, DateTimeOffset enrolledAt)
        => new()
        {
            ClassGroupId = classGroupId,
            LearnerUserId = learnerUserId,
            EnrolledAt = enrolledAt,
            Status = ClassGroupMemberStatus.Active
        };

    public void Leave(DateTimeOffset leftAt)
    {
        Status = ClassGroupMemberStatus.Left;
        LeftAt = leftAt;
    }
}
