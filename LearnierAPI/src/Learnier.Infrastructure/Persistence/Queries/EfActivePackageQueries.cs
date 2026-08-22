using Learnier.Application.Common.Abstractions;
using Learnier.Domain.Billing;
using Learnier.Domain.Catalog;
using Microsoft.EntityFrameworkCore;

namespace Learnier.Infrastructure.Persistence.Queries;

internal sealed class EfActivePackageQueries(AppDbContext context, IClock clock)
    : IActivePackageQueries
{
    public async Task<IReadOnlyList<ActivePackageAccess>> ListForUserAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var subscriptions = await context.Subscriptions
            .AsNoTracking()
            .Include(subscription => subscription.PlanPrice)
                .ThenInclude(price => price.Plan)
                    .ThenInclude(plan => plan.Entitlements)
            .Where(subscription =>
                subscription.SubscriberUserId == userId
                && subscription.Status == SubscriptionStatus.Active
                && subscription.CurrentPeriodStart <= clock.UtcNow
                && subscription.CurrentPeriodEnd > clock.UtcNow)
            .ToListAsync(cancellationToken);

        var result = new List<ActivePackageAccess>();
        foreach (var subscription in subscriptions)
        {
            var plan = subscription.PlanPrice.Plan;
            var subjects = plan.CatalogAccess == CatalogAccess.All
                ? await context.Subjects.AsNoTracking()
                    .Where(subject => subject.Status == SubjectStatus.Active)
                    .Select(subject => new { subject.Id, subject.Name })
                    .ToListAsync(cancellationToken)
                : await context.PlanSubjectAccess.AsNoTracking()
                    .Where(access => access.PlanId == plan.Id)
                    .Select(access => new { access.Subject.Id, access.Subject.Name })
                    .ToListAsync(cancellationToken);

            var currentGrant = await context.CreditLedger
                .Where(entry => entry.SubscriptionId == subscription.Id
                                && entry.LearnerUserId == userId
                                && entry.TransactionType == CreditTransactionType.PeriodGrant
                                && entry.PeriodStart <= clock.UtcNow
                                && (entry.ExpiresAt == null || entry.ExpiresAt > clock.UtcNow))
                .OrderByDescending(entry => entry.PeriodStart ?? entry.CreatedAt)
                .Select(entry => new { entry.PeriodStart, entry.CreatedAt })
                .FirstOrDefaultAsync(cancellationToken);

            var remainingCredits = currentGrant is null
                ? 0
                : await context.CreditLedger
                    .Where(entry => entry.SubscriptionId == subscription.Id
                                    && entry.LearnerUserId == userId
                                    && (entry.PeriodStart == (currentGrant.PeriodStart ?? currentGrant.CreatedAt)
                                        || (currentGrant.PeriodStart == null
                                            && entry.PeriodStart == null)))
                    .SumAsync(entry => (int?)entry.Quantity, cancellationToken) ?? 0;

            var durationMonths = subscription.PlanPrice.BillingInterval == BillingInterval.Year
                ? subscription.PlanPrice.BillingIntervalCount * 12
                : subscription.PlanPrice.BillingIntervalCount;
            var entitlement = plan.Entitlements.FirstOrDefault(item =>
                item.EntitlementType == EntitlementType.LessonCredit
                && item.SessionType == Learnier.Domain.Scheduling.SessionType.Private);
            var totalCredits = entitlement?.Quantity ?? Math.Max(remainingCredits, 0);
            var lessonsPerWeek = totalCredits > 0
                ? Math.Max(1, totalCredits / 4)
                : 3;

            result.AddRange(subjects.Select(subject => new ActivePackageAccess(
                subscription.Id, plan.Name, subject.Id, subject.Name,
                subscription.StartsAt, subscription.CurrentPeriodEnd, remainingCredits,
                totalCredits, lessonsPerWeek, durationMonths,
                plan.LessonDurationMinutes ?? 50)));
        }

        return result;
    }
}
