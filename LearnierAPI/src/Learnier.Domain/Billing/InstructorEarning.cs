using Learnier.Domain.Common;

namespace Learnier.Domain.Billing;

/// <summary>Tamamlanan bir ders için değişmez eğitmen kazanç kaydı.</summary>
public sealed class InstructorEarning : Entity, IAuditableEntity
{
    private InstructorEarning() => Currency = string.Empty;

    public Guid SessionId { get; private set; }
    public Guid InstructorProfileId { get; private set; }
    public Guid SubjectId { get; private set; }
    public int LessonDurationMinutes { get; private set; }
    public decimal GrossAmount { get; private set; }
    public decimal PenaltyPercentage { get; private set; }
    public decimal PenaltyAmount { get; private set; }
    public decimal NetAmount { get; private set; }
    public string Currency { get; private set; }
    public DateTimeOffset EarnedAt { get; private set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }

    public static InstructorEarning Create(
        Guid sessionId,
        Guid instructorProfileId,
        Guid subjectId,
        int lessonDurationMinutes,
        decimal grossAmount,
        decimal penaltyPercentage,
        string currency,
        DateTimeOffset earnedAt)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(grossAmount);
        if (penaltyPercentage is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(penaltyPercentage));
        }
        ArgumentException.ThrowIfNullOrWhiteSpace(currency);

        var penaltyAmount = decimal.Round(
            grossAmount * penaltyPercentage / 100m,
            2,
            MidpointRounding.AwayFromZero);

        return new InstructorEarning
        {
            SessionId = sessionId,
            InstructorProfileId = instructorProfileId,
            SubjectId = subjectId,
            LessonDurationMinutes = lessonDurationMinutes,
            GrossAmount = grossAmount,
            PenaltyPercentage = penaltyPercentage,
            PenaltyAmount = penaltyAmount,
            NetAmount = grossAmount - penaltyAmount,
            Currency = currency.Trim().ToUpperInvariant(),
            EarnedAt = earnedAt
        };
    }
}
