using Learnier.Domain.Common;

namespace Learnier.Domain.Billing;

/// <summary>Eğitmenin tamamlanacak sonraki dersine taşınan penalty seviyesi.</summary>
public sealed class InstructorPenaltyState : Entity, IAuditableEntity
{
    private InstructorPenaltyState() { }

    public Guid InstructorProfileId { get; private set; }
    public int Level { get; private set; }
    public decimal? PendingPercentage { get; private set; }
    public Guid? LastCancelledSessionId { get; private set; }
    public DateTimeOffset? LastPenaltyAt { get; private set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }

    public static InstructorPenaltyState Create(Guid instructorProfileId)
        => new() { InstructorProfileId = instructorProfileId };

    public void RegisterLateCancellation(
        Guid sessionId,
        decimal pendingPercentage,
        DateTimeOffset occurredAt,
        int? maximumLevel = null)
    {
        if (LastCancelledSessionId == sessionId)
        {
            return;
        }

        if (pendingPercentage is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(pendingPercentage));
        }

        if (maximumLevel is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumLevel));
        }

        Level = maximumLevel is { } cap
            ? Math.Min(Level + 1, cap)
            : Level + 1;
        PendingPercentage = pendingPercentage;
        LastCancelledSessionId = sessionId;
        LastPenaltyAt = occurredAt;
    }

    public void Clear()
    {
        Level = 0;
        PendingPercentage = null;
        LastCancelledSessionId = null;
        LastPenaltyAt = null;
    }
}
