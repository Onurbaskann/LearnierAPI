using Learnier.Domain.Common;

namespace Learnier.Domain.Billing;

/// <summary>Kurumun öğrenci iadesi ve eğitmen geç iptal sınırları.</summary>
public sealed class CancellationPolicy : Entity, IAuditableEntity, ITenantScoped
{
    public const int DefaultStudentRefundCutoffMinutes = 60;
    public const int DefaultInstructorPenaltyCutoffMinutes = 240;

    private CancellationPolicy() { }

    public Guid OrganizationId { get; private set; }
    public int StudentRefundCutoffMinutes { get; private set; }
    public int InstructorPenaltyCutoffMinutes { get; private set; }
    public int Version { get; private set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }

    public static CancellationPolicy Create(
        Guid organizationId,
        int studentRefundCutoffMinutes,
        int instructorPenaltyCutoffMinutes)
    {
        if (organizationId == Guid.Empty)
        {
            throw new ArgumentException("OrganizationId boş olamaz.", nameof(organizationId));
        }

        Validate(studentRefundCutoffMinutes, instructorPenaltyCutoffMinutes);

        return new CancellationPolicy
        {
            OrganizationId = organizationId,
            StudentRefundCutoffMinutes = studentRefundCutoffMinutes,
            InstructorPenaltyCutoffMinutes = instructorPenaltyCutoffMinutes,
            Version = 1
        };
    }

    public void Update(int studentRefundCutoffMinutes, int instructorPenaltyCutoffMinutes)
    {
        Validate(studentRefundCutoffMinutes, instructorPenaltyCutoffMinutes);
        StudentRefundCutoffMinutes = studentRefundCutoffMinutes;
        InstructorPenaltyCutoffMinutes = instructorPenaltyCutoffMinutes;
        Version++;
    }

    private static void Validate(int studentMinutes, int instructorMinutes)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(studentMinutes, 0);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(studentMinutes, 10_080);
        ArgumentOutOfRangeException.ThrowIfLessThan(instructorMinutes, 0);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(instructorMinutes, 10_080);
    }
}
