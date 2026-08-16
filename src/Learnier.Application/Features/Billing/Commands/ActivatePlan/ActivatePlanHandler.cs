using Learnier.Application.Common.Abstractions;
using Learnier.Application.Common.Results;
using Learnier.Domain.Billing;

namespace Learnier.Application.Features.Billing.Commands.ActivatePlan;

/// <summary>
/// Plani satisa acar.
/// </summary>
/// <remarks>
/// Taslak plan satilamaz: fiyati ve hak tanimlari eksik olabilir. Aktiflestirme
/// ayri bir adim oldugu icin plan, hazir olmadan musteriye gorunmez.
/// </remarks>
public sealed class ActivatePlanHandler(
    IPlanRepository plans,
    ICurrentTenant currentTenant,
    IUnitOfWork unitOfWork)
{
    public async Task<Result> Handle(Guid planId, CancellationToken cancellationToken)
    {
        if (!currentTenant.HasTenant)
        {
            return Result.Failure(BillingErrors.OrganizationContextRequired);
        }

        var plan = await plans.FindPlanAsync(planId, includeDetails: true, cancellationToken);

        if (plan is null)
        {
            return Result.Failure(BillingErrors.PlanNotFound);
        }

        // Fiyatsiz plan satilamaz; abonelik acilirken fiyat kimligi zorunlu.
        if (plan.Prices.All(p => p.Status is not PlanPriceStatus.Active))
        {
            return Result.Failure(BillingErrors.PlanHasNoActivePrice);
        }

        // Hak tanimi olmayan plan aboneye hicbir sey vermez.
        if (plan.Entitlements.Count is 0)
        {
            return Result.Failure(BillingErrors.PlanHasNoEntitlement);
        }

        plan.Activate();

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
