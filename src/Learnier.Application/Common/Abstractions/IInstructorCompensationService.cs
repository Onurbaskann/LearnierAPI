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
}
