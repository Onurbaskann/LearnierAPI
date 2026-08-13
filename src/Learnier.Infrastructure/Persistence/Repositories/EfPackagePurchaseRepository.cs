using Learnier.Application.Common.Abstractions;
using Learnier.Domain.Billing;
using Learnier.Domain.Catalog;
using Microsoft.EntityFrameworkCore;

namespace Learnier.Infrastructure.Persistence.Repositories;

internal sealed class EfPackagePurchaseRepository(AppDbContext context)
    : IPackagePurchaseRepository
{
    public Task<Subject?> FindSubjectAsync(Guid subjectId, CancellationToken cancellationToken)
        => context.Subjects.FirstOrDefaultAsync(
            subject => subject.Id == subjectId && subject.Status == SubjectStatus.Active,
            cancellationToken);

    public Task<SubscriptionPlan?> FindPlanAsync(
        Guid organizationId,
        string planName,
        CancellationToken cancellationToken)
        => context.SubscriptionPlans
            .Include(plan => plan.Prices)
            .Include(plan => plan.Entitlements)
            .FirstOrDefaultAsync(
                plan => plan.OrganizationId == organizationId && plan.Name == planName,
                cancellationToken);

    public void AddPlan(SubscriptionPlan plan) => context.SubscriptionPlans.Add(plan);

    public void AddSubjectAccess(PlanSubjectAccess access) => context.PlanSubjectAccess.Add(access);

    public void AddSubscription(Subscription subscription) => context.Subscriptions.Add(subscription);

    public void AddCredit(CreditLedgerEntry credit) => context.CreditLedger.Add(credit);
}
