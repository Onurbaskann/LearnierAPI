using Learnier.Domain.Common;

namespace Learnier.Domain.Billing;

/// <summary>Bir ders alanı ve süre için eğitmene ödenecek sabit ders ücreti.</summary>
public sealed class InstructorCompensationRate : AggregateRoot, IAuditableEntity, ITenantScoped
{
    private InstructorCompensationRate() => Currency = string.Empty;

    public Guid OrganizationId { get; private set; }
    public Guid SubjectId { get; private set; }
    public int LessonDurationMinutes { get; private set; }
    public decimal Amount { get; private set; }
    public string Currency { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }

    public static InstructorCompensationRate Create(
        Guid organizationId,
        Guid subjectId,
        int lessonDurationMinutes,
        decimal amount,
        string currency)
    {
        if (lessonDurationMinutes is not (30 or 50))
        {
            throw new ArgumentOutOfRangeException(nameof(lessonDurationMinutes));
        }

        ArgumentOutOfRangeException.ThrowIfNegative(amount);
        ArgumentException.ThrowIfNullOrWhiteSpace(currency);

        return new InstructorCompensationRate
        {
            OrganizationId = organizationId,
            SubjectId = subjectId,
            LessonDurationMinutes = lessonDurationMinutes,
            Amount = amount,
            Currency = currency.Trim().ToUpperInvariant(),
            IsActive = true
        };
    }

    public void Update(decimal amount, string currency)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);
        ArgumentException.ThrowIfNullOrWhiteSpace(currency);
        Amount = amount;
        Currency = currency.Trim().ToUpperInvariant();
        IsActive = true;
    }

    public void Deactivate() => IsActive = false;
}
