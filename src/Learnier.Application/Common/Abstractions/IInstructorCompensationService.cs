using Learnier.Application.Common.Results;

namespace Learnier.Application.Common.Abstractions;

public interface IInstructorCompensationService
{
    Task<Result> RegisterLateCancellationAsync(
        Guid instructorProfileId,
        Guid sessionId,
        CancellationToken cancellationToken);

    Task<Result> CreateEarningsAsync(Guid sessionId, CancellationToken cancellationToken);

    Task<Result<Guid>> ConfigureRateAsync(
        Guid subjectId,
        int lessonDurationMinutes,
        decimal amount,
        string currency,
        CancellationToken cancellationToken);

    Task<Result> ConfigurePenaltyStepsAsync(
        IReadOnlyList<decimal> percentages,
        CancellationToken cancellationToken);

    Task<Result<CompensationSettings>> GetSettingsAsync(
        CancellationToken cancellationToken);

    Task<Result<InstructorPenaltyHistory>> GetPenaltyHistoryAsync(
        Guid instructorProfileId,
        CancellationToken cancellationToken);

    Task<Result> WaivePenaltyAsync(
        Guid instructorProfileId,
        string reason,
        CancellationToken cancellationToken);
}

public sealed record InstructorPenaltyEventItem(
    Guid Id,
    string EventType,
    Guid? SessionId,
    Guid? EarningId,
    int Level,
    decimal Percentage,
    string Reason,
    DateTimeOffset OccurredAt,
    Guid? ActorUserId);

public sealed record InstructorPenaltyHistory(
    Guid InstructorProfileId,
    int CurrentLevel,
    decimal PendingPercentage,
    IReadOnlyList<InstructorPenaltyEventItem> Events);

public sealed record CompensationRateItem(
    Guid Id,
    Guid SubjectId,
    string SubjectName,
    int LessonDurationMinutes,
    decimal Amount,
    string Currency,
    bool IsActive);

public sealed record CompensationPenaltyStepItem(int Level, decimal Percentage);

public sealed record CompensationSettings(
    IReadOnlyList<CompensationRateItem> Rates,
    IReadOnlyList<CompensationPenaltyStepItem> PenaltySteps,
    bool UsesDefaultPenaltySteps);
