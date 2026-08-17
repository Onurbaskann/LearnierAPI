using Learnier.Application.Common.Abstractions;
using Learnier.Domain.Billing;
using Learnier.Domain.Scheduling;
using Learnier.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Learnier.Infrastructure.Billing;

internal sealed class CreditPeriodRenewalProcessor(
    AppDbContext context,
    IClock clock) : ICreditPeriodRenewalProcessor
{
    public async Task<CreditPeriodRenewalResult> ProcessDueAsync(
        int batchSize,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(batchSize);

        var now = clock.UtcNow;
        var candidateIds = await (
                from subscription in context.Subscriptions.IgnoreQueryFilters().AsNoTracking()
                join price in context.PlanPrices.IgnoreQueryFilters()
                    on subscription.PlanPriceId equals price.Id
                join plan in context.SubscriptionPlans.IgnoreQueryFilters()
                    on price.PlanId equals plan.Id
                where subscription.Status == SubscriptionStatus.Active
                      && subscription.SubscriberUserId != null
                      && context.PlanEntitlements.IgnoreQueryFilters().Any(entitlement =>
                          entitlement.PlanId == plan.Id
                          && entitlement.EntitlementType == EntitlementType.LessonCredit
                          && entitlement.SessionType == SessionType.Private
                          && entitlement.ResetPeriod == EntitlementResetPeriod.Month)
                      && context.CreditLedger.IgnoreQueryFilters().Any(entry =>
                          entry.SubscriptionId == subscription.Id
                          && entry.TransactionType == CreditTransactionType.PeriodGrant)
                      && !context.CreditLedger.IgnoreQueryFilters().Any(entry =>
                          entry.SubscriptionId == subscription.Id
                          && entry.TransactionType == CreditTransactionType.PeriodGrant
                          && (entry.ExpiresAt == null || entry.ExpiresAt > now))
                orderby subscription.Id
                select subscription.Id)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

        var renewedPeriods = 0;
        var expiredCredits = 0;
        var grantedCredits = 0;
        var endedSubscriptions = 0;

        foreach (var subscriptionId in candidateIds)
        {
            await using var transaction = await context.BeginTransactionAsync(cancellationToken);

            // Birden fazla worker ayni anda calisirsa kilitli abonelik beklenmez;
            // diger worker kalan aboneliklerle devam edebilir.
            var subscription = await context.Subscriptions
                .FromSqlInterpolated($$"""
                    SELECT *
                    FROM subscriptions
                    WHERE id = {{subscriptionId}}
                    FOR UPDATE SKIP LOCKED
                    """)
                .IgnoreQueryFilters()
                .SingleOrDefaultAsync(cancellationToken);

            if (subscription is null || subscription.Status != SubscriptionStatus.Active)
            {
                await transaction.RollbackAsync(cancellationToken);
                context.ChangeTracker.Clear();
                continue;
            }

            // Yenilenecek hak plan uzerindeki denormalize alandan degil hak
            // tanimlarindan okunur: yonetici panelinden acilan planlar o alani
            // doldurmaz, yalnizca demo akisi doldururdu.
            //
            // Ayni plan hem 30 hem 50 dakikalik birebir kredi tasiyabilir. Defterde
            // sure kirilimi yoktur, bakiye abonelik basina tutulur; bu yuzden ikisi
            // toplanir - satin almada verilen ilk donem hakkiyla ayni davranis.
            var monthlyCredits = await (
                    from price in context.PlanPrices.IgnoreQueryFilters()
                    join entitlement in context.PlanEntitlements.IgnoreQueryFilters()
                        on price.PlanId equals entitlement.PlanId
                    where price.Id == subscription.PlanPriceId
                          && entitlement.EntitlementType == EntitlementType.LessonCredit
                          && entitlement.SessionType == SessionType.Private
                          && entitlement.ResetPeriod == EntitlementResetPeriod.Month
                    select entitlement.Quantity ?? 0)
                .SumAsync(cancellationToken);

            if (monthlyCredits <= 0 || subscription.SubscriberUserId is not { } learnerUserId)
            {
                await transaction.RollbackAsync(cancellationToken);
                context.ChangeTracker.Clear();
                continue;
            }

            var currentGrant = await context.CreditLedger
                .IgnoreQueryFilters()
                .Where(entry => entry.SubscriptionId == subscription.Id
                                && entry.SessionType == SessionType.Private
                                && entry.TransactionType == CreditTransactionType.PeriodGrant)
                .OrderByDescending(entry => entry.PeriodStart ?? entry.CreatedAt)
                .FirstAsync(cancellationToken);

            int? trackedPeriodBalance = null;

            while (currentGrant.ExpiresAt is { } periodEnd && periodEnd <= now)
            {
                var isLegacyPeriod = currentGrant.PeriodStart is null;
                var currentPeriodStart = currentGrant.PeriodStart ?? currentGrant.CreatedAt;
                var periodBalance = trackedPeriodBalance
                    ?? await context.CreditLedger
                        .IgnoreQueryFilters()
                        .Where(entry => entry.SubscriptionId == subscription.Id
                                        && entry.LearnerUserId == learnerUserId
                                        && entry.SessionType == SessionType.Private
                                        && (entry.PeriodStart == currentPeriodStart
                                            || (isLegacyPeriod && entry.PeriodStart == null)))
                        .SumAsync(entry => (int?)entry.Quantity, cancellationToken) ?? 0;

                if (periodBalance > 0)
                {
                    context.CreditLedger.Add(CreditLedgerEntry.Expire(
                        subscription.Id,
                        learnerUserId,
                        SessionType.Private,
                        periodBalance,
                        now,
                        currentPeriodStart));
                    expiredCredits += periodBalance;
                }

                if (periodEnd >= subscription.CurrentPeriodEnd)
                {
                    subscription.Cancel(periodEnd, immediately: true);
                    endedSubscriptions++;
                    break;
                }

                var nextPeriodEnd = periodEnd.AddMonths(1);
                if (nextPeriodEnd > subscription.CurrentPeriodEnd)
                {
                    nextPeriodEnd = subscription.CurrentPeriodEnd;
                }

                currentGrant = CreditLedgerEntry.Grant(
                    subscription.Id,
                    learnerUserId,
                    SessionType.Private,
                    monthlyCredits,
                    now,
                    nextPeriodEnd,
                    periodEnd);
                context.CreditLedger.Add(currentGrant);
                renewedPeriods++;
                grantedCredits += monthlyCredits;
                trackedPeriodBalance = monthlyCredits;
            }

            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            context.ChangeTracker.Clear();
        }

        return new CreditPeriodRenewalResult(
            candidateIds.Count,
            renewedPeriods,
            expiredCredits,
            grantedCredits,
            endedSubscriptions);
    }
}
