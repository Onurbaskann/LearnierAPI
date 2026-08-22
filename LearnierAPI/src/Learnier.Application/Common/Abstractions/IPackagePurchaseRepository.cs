using Learnier.Domain.Billing;
using Learnier.Domain.Catalog;

namespace Learnier.Application.Common.Abstractions;

public interface IPackagePurchaseRepository
{
    Task<Subject?> FindSubjectAsync(Guid subjectId, CancellationToken cancellationToken);

    Task<SubscriptionPlan?> FindPlanAsync(
        Guid organizationId,
        string planName,
        CancellationToken cancellationToken);

    void AddPlan(SubscriptionPlan plan);

    void AddSubjectAccess(PlanSubjectAccess access);

    void AddSubscription(Subscription subscription);

    void AddCredit(CreditLedgerEntry credit);
}
