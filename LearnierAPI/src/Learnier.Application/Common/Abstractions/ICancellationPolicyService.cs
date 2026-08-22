using Learnier.Application.Common.Results;

namespace Learnier.Application.Common.Abstractions;

public sealed record CancellationPolicySnapshot(
    int StudentRefundCutoffMinutes,
    int InstructorPenaltyCutoffMinutes,
    int Version);

public interface ICancellationPolicyService
{
    Task<Result<CancellationPolicySnapshot>> GetCurrentAsync(CancellationToken cancellationToken);

    Task<Result<CancellationPolicySnapshot>> ConfigureAsync(
        int studentRefundCutoffMinutes,
        int instructorPenaltyCutoffMinutes,
        CancellationToken cancellationToken);
}
