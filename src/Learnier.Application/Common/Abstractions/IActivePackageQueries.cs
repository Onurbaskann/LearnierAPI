namespace Learnier.Application.Common.Abstractions;

public sealed record ActivePackageAccess(
    Guid SubscriptionId,
    string PlanName,
    Guid SubjectId,
    string SubjectName,
    DateTimeOffset StartsAt,
    DateTimeOffset CurrentPeriodEnd,
    int RemainingCredits);

public interface IActivePackageQueries
{
    Task<IReadOnlyList<ActivePackageAccess>> ListForUserAsync(
        Guid userId,
        CancellationToken cancellationToken);
}
