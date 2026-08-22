using Learnier.Domain.Scheduling;

namespace Learnier.Application.Common.Abstractions;

public interface IMeetingRepository
{
    Task<Meeting?> FindBySessionAsync(Guid sessionId, CancellationToken cancellationToken);

    void Add(Meeting meeting);
}

public interface IMeetingProvisioningProcessor
{
    Task<int> ProcessBatchAsync(CancellationToken cancellationToken);

    Task<int> ProcessCancellationBatchAsync(CancellationToken cancellationToken);
}
