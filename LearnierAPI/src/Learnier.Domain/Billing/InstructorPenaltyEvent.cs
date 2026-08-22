using Learnier.Domain.Common;

namespace Learnier.Domain.Billing;

public enum InstructorPenaltyEventType
{
    LateCancellation,
    Applied,
    Waived
}

/// <summary>Eğitmen cezasındaki değişiklikleri değiştirilmeden saklayan denetim kaydı.</summary>
public sealed class InstructorPenaltyEvent : Entity, IAuditableEntity, ITenantScoped
{
    private InstructorPenaltyEvent() => Reason = string.Empty;

    public Guid OrganizationId { get; private set; }
    public Guid InstructorProfileId { get; private set; }
    public Guid? SessionId { get; private set; }
    public Guid? EarningId { get; private set; }
    public InstructorPenaltyEventType EventType { get; private set; }
    public int Level { get; private set; }
    public decimal Percentage { get; private set; }
    public string Reason { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }
    public Guid? ActorUserId { get; private set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }

    public static InstructorPenaltyEvent LateCancellation(
        Guid organizationId,
        Guid instructorProfileId,
        Guid sessionId,
        int level,
        decimal percentage,
        DateTimeOffset occurredAt)
        => Create(
            organizationId, instructorProfileId, sessionId, null,
            InstructorPenaltyEventType.LateCancellation, level, percentage,
            string.Empty, occurredAt, null);

    public static InstructorPenaltyEvent Applied(
        Guid organizationId,
        Guid instructorProfileId,
        Guid sessionId,
        Guid earningId,
        int level,
        decimal percentage,
        DateTimeOffset occurredAt)
        => Create(
            organizationId, instructorProfileId, sessionId, earningId,
            InstructorPenaltyEventType.Applied, level, percentage,
            string.Empty, occurredAt, null);

    public static InstructorPenaltyEvent Waived(
        Guid organizationId,
        Guid instructorProfileId,
        int level,
        decimal percentage,
        string reason,
        DateTimeOffset occurredAt,
        Guid actorUserId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        return Create(
            organizationId, instructorProfileId, null, null,
            InstructorPenaltyEventType.Waived, level, percentage,
            reason.Trim(), occurredAt, actorUserId);
    }

    private static InstructorPenaltyEvent Create(
        Guid organizationId,
        Guid instructorProfileId,
        Guid? sessionId,
        Guid? earningId,
        InstructorPenaltyEventType eventType,
        int level,
        decimal percentage,
        string reason,
        DateTimeOffset occurredAt,
        Guid? actorUserId)
    {
        if (organizationId == Guid.Empty || instructorProfileId == Guid.Empty)
        {
            throw new ArgumentException("Kurum ve eğitmen kimlikleri boş olamaz.");
        }
        ArgumentOutOfRangeException.ThrowIfNegative(level);
        if (percentage is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(percentage));
        }

        return new InstructorPenaltyEvent
        {
            OrganizationId = organizationId,
            InstructorProfileId = instructorProfileId,
            SessionId = sessionId,
            EarningId = earningId,
            EventType = eventType,
            Level = level,
            Percentage = percentage,
            Reason = reason,
            OccurredAt = occurredAt,
            ActorUserId = actorUserId
        };
    }
}
