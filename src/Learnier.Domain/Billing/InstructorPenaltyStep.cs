using Learnier.Domain.Common;

namespace Learnier.Domain.Billing;

/// <summary>Art arda geç iptal sayısına uygulanacak yönetilebilir kesinti basamağı.</summary>
public sealed class InstructorPenaltyStep : Entity, IAuditableEntity, ITenantScoped
{
    private InstructorPenaltyStep() { }

    public Guid OrganizationId { get; private set; }
    public int Level { get; private set; }
    public decimal Percentage { get; private set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }

    public static InstructorPenaltyStep Create(Guid organizationId, int level, decimal percentage)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(level);
        if (percentage is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(percentage));
        }

        return new InstructorPenaltyStep
        {
            OrganizationId = organizationId,
            Level = level,
            Percentage = percentage
        };
    }

    public void Update(decimal percentage)
    {
        if (percentage is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(percentage));
        }

        Percentage = percentage;
    }
}
