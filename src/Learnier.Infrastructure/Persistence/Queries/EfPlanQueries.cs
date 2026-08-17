using Learnier.Application.Common.Abstractions;
using Learnier.Domain.Billing;
using Microsoft.EntityFrameworkCore;

namespace Learnier.Infrastructure.Persistence.Queries;

/// <inheritdoc cref="IPlanQueries"/>
/// <remarks>
/// Kurum ayrimi <c>SubscriptionPlans</c> uzerindeki global kiraci filtresinden
/// gelir; erisim tablolari plan uzerinden daraldigi icin ayrica filtre gerekmez.
/// </remarks>
internal sealed class EfPlanQueries(AppDbContext context) : IPlanQueries
{
    public async Task<IReadOnlyList<PlanDetail>> ListAsync(CancellationToken cancellationToken)
    {
        // Siralama projeksiyondan once yapilir: PlanDetail kaydinin alanlari uzerinden
        // ORDER BY cevrilemiyor.
        var plans = await Project(
                context.SubscriptionPlans.AsNoTracking().OrderBy(plan => plan.Name))
            .ToListAsync(cancellationToken);

        return plans.Select(SortAccess).ToList();
    }

    public async Task<PlanDetail?> FindAsync(Guid planId, CancellationToken cancellationToken)
    {
        var plan = await Project(
                context.SubscriptionPlans.AsNoTracking().Where(item => item.Id == planId))
            .SingleOrDefaultAsync(cancellationToken);

        return plan is null ? null : SortAccess(plan);
    }

    /// <summary>
    /// Kapsam listelerini ada gore siralar.
    /// </summary>
    /// <remarks>
    /// Siralama bellekte yapilir: alan ve egitim adlari kendi kiraci filtreleri olan
    /// navigasyonlardan gelir ve ic ice bir alt sorgunun <c>ORDER BY</c>'inda
    /// kullanildiklarinda EF sorguyu ceviremiyor.
    /// </remarks>
    private static PlanDetail SortAccess(PlanDetail plan)
        => plan with
        {
            SubjectAccess = [.. plan.SubjectAccess.OrderBy(access => access.Name, StringComparer.CurrentCulture)],
            CourseAccess = [.. plan.CourseAccess.OrderBy(access => access.Name, StringComparer.CurrentCulture)]
        };

    public async Task<IReadOnlyList<CatalogPlanItem>> ListPurchasableAsync(
        CancellationToken cancellationToken)
    {
        var plans = await context.SubscriptionPlans
            .AsNoTracking()
            .Where(plan => plan.Status == PlanStatus.Active
                           // Satin alma akisinin ortuk urettigi planlar kataloga girmez:
                           // kimse onlari satisa acmadi.
                           && !plan.IsSystemGenerated
                           && plan.Prices.Any(price => price.Status == PlanPriceStatus.Active)
                           && plan.Entitlements.Any())
            .OrderBy(plan => plan.Name)
            .Select(plan => new
            {
                plan.Id,
                plan.Name,
                plan.Description,
                plan.CatalogAccess,
                // Ayni plan farkli periyotlarda birden fazla aktif fiyat tasiyabilir;
                // katalog en erken baslayani gosterir. Siralama projeksiyondan once
                // yapilir: kayit alanlari uzerinden ORDER BY cevrilemiyor.
                ActivePrice = plan.Prices
                    .Where(price => price.Status == PlanPriceStatus.Active)
                    .OrderBy(price => price.ValidFrom)
                    .Select(price => new PlanPriceItem(
                        price.Id,
                        price.Currency,
                        price.Amount,
                        price.BillingInterval,
                        price.BillingIntervalCount,
                        price.Status,
                        price.ValidFrom,
                        price.ValidUntil))
                    .First(),
                Entitlements = plan.Entitlements
                    .OrderBy(entitlement => entitlement.SessionType)
                    .ThenBy(entitlement => entitlement.LessonDurationMinutes)
                    .Select(entitlement => new PlanEntitlementItem(
                        entitlement.Id,
                        entitlement.EntitlementType,
                        entitlement.SessionType,
                        entitlement.Quantity,
                        entitlement.ResetPeriod,
                        entitlement.LessonDurationMinutes))
                    .ToList(),
                SubjectAccess = context.PlanSubjectAccess
                    .Where(access => access.PlanId == plan.Id)
                    .Select(access => new PlanAccessItem(access.SubjectId, access.Subject.Name))
                    .ToList(),
                CourseAccess = context.PlanCourseAccess
                    .Where(access => access.PlanId == plan.Id)
                    .Select(access => new PlanAccessItem(access.CourseId, access.Course.Title))
                    .ToList()
            })
            .ToListAsync(cancellationToken);

        return plans
            .Select(plan => new CatalogPlanItem(
                plan.Id,
                plan.Name,
                plan.Description,
                plan.CatalogAccess,
                plan.ActivePrice,
                plan.Entitlements,
                [.. plan.SubjectAccess.OrderBy(access => access.Name, StringComparer.CurrentCulture)],
                [.. plan.CourseAccess.OrderBy(access => access.Name, StringComparer.CurrentCulture)]))
            .ToList();
    }

    private IQueryable<PlanDetail> Project(IQueryable<SubscriptionPlan> plans)
        => plans.Select(plan => new PlanDetail(
            plan.Id,
            plan.Name,
            plan.Description,
            plan.CatalogAccess,
            plan.Status,
            plan.IsSystemGenerated,
            plan.Prices
                .Where(price => price.Status == PlanPriceStatus.Active)
                .OrderBy(price => price.ValidFrom)
                .Select(price => new PlanPriceItem(
                    price.Id,
                    price.Currency,
                    price.Amount,
                    price.BillingInterval,
                    price.BillingIntervalCount,
                    price.Status,
                    price.ValidFrom,
                    price.ValidUntil))
                .FirstOrDefault(),
            // Fiyat gecmisi yeniden eskiye: yonetici once yururlukteki tutari gorur.
            plan.Prices
                .OrderByDescending(price => price.ValidFrom)
                .Select(price => new PlanPriceItem(
                    price.Id,
                    price.Currency,
                    price.Amount,
                    price.BillingInterval,
                    price.BillingIntervalCount,
                    price.Status,
                    price.ValidFrom,
                    price.ValidUntil))
                .ToList(),
            plan.Entitlements
                .OrderBy(entitlement => entitlement.SessionType)
                .ThenBy(entitlement => entitlement.LessonDurationMinutes)
                .Select(entitlement => new PlanEntitlementItem(
                    entitlement.Id,
                    entitlement.EntitlementType,
                    entitlement.SessionType,
                    entitlement.Quantity,
                    entitlement.ResetPeriod,
                    entitlement.LessonDurationMinutes))
                .ToList(),
            context.PlanSubjectAccess
                .Where(access => access.PlanId == plan.Id)
                .Select(access => new PlanAccessItem(access.SubjectId, access.Subject.Name))
                .ToList(),
            context.PlanCourseAccess
                .Where(access => access.PlanId == plan.Id)
                .Select(access => new PlanAccessItem(access.CourseId, access.Course.Title))
                .ToList(),
            plan.CreatedAt,
            plan.UpdatedAt));
}
