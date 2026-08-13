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

            var remainingCredits = await context.CreditLedger
                .Where(entry => entry.SubscriptionId == subscription.Id && entry.LearnerUserId == userId)
                .SumAsync(entry => (int?)entry.Quantity, cancellationToken) ?? 0;

            result.AddRange(subjects.Select(subject => new ActivePackageAccess(
                subscription.Id, plan.Name, subject.Id, subject.Name,
                subscription.StartsAt, subscription.CurrentPeriodEnd, remainingCredits)));
        }

        return result;
    }
}
