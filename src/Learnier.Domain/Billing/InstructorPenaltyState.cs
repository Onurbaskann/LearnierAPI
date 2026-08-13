using Learnier.Domain.Common;

namespace Learnier.Domain.Billing;

/// <summary>Eğitmenin tamamlanacak sonraki dersine taşınan penalty seviyesi.</summary>
public sealed class InstructorPenaltyState : Entity, IAuditableEntity
{
    private InstructorPenaltyState() { }

    public Guid InstructorProfileId { get; private set; }
    public int Level { get; private set; }
    public Guid? LastCancelledSessionId { get; private set; }
    public DateTimeOffset? LastPenaltyAt { get; private set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }

    public static InstructorPenaltyState Create(Guid instructorProfileId)
        => new() { InstructorProfileId = instructorProfileId };

    public void RegisterLateCancellation(Guid sessionId, DateTimeOffset occurredAt)
    {
        if (LastCancelledSessionId == sessionId)
        {
            return;
        }

        Level++;
        LastCancelledSessionId = sessionId;
        LastPenaltyAt = occurredAt;
    }

    public void Clear()
    {
        Level = 0;
        LastCancelledSessionId = null;
        LastPenaltyAt = null;
    }
}
