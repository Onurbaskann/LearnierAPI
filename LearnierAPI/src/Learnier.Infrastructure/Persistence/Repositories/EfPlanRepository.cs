using Learnier.Application.Common.Abstractions;
using Learnier.Domain.Billing;
using Microsoft.EntityFrameworkCore;

namespace Learnier.Infrastructure.Persistence.Repositories;

/// <inheritdoc cref="IPlanRepository"/>
internal sealed class EfPlanRepository(AppDbContext context) : IPlanRepository
{
    public Task<SubscriptionPlan?> FindPlanAsync(
        Guid planId,
        bool includeDetails,
        CancellationToken cancellationToken)
    {
        var query = context.SubscriptionPlans.AsQueryable();

        if (includeDetails)
        {
            query = query
                .Include(plan => plan.Prices)
                .Include(plan => plan.Entitlements);
        }

        return query.FirstOrDefaultAsync(plan => plan.Id == planId, cancellationToken);
    }

    public void AddPlan(SubscriptionPlan plan) => context.SubscriptionPlans.Add(plan);

    public void AddSubjectAccess(PlanSubjectAccess access) => context.PlanSubjectAccess.Add(access);

    public void AddCourseAccess(PlanCourseAccess access) => context.PlanCourseAccess.Add(access);
}
