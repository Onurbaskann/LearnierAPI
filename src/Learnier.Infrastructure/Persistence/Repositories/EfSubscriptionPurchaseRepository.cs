using Learnier.Application.Common.Abstractions;
using Learnier.Domain.Billing;
using Microsoft.EntityFrameworkCore;

namespace Learnier.Infrastructure.Persistence.Repositories;

/// <inheritdoc cref="ISubscriptionPurchaseRepository"/>
internal sealed class EfSubscriptionPurchaseRepository(AppDbContext context)
    : ISubscriptionPurchaseRepository
{
    /// <remarks>
    /// Sorgu <c>PlanPrices</c> yerine kiraci filtresi tasiyan <c>SubscriptionPlans</c>
    /// uzerinden kurulur: fiyat kaydinin kendisi kiraci kapsamli degildir, plani ise
    /// oyledir. Boylece baska kurumun fiyat kimligi yazilsa bile sonuc bos doner.
    /// </remarks>
    public Task<SubscriptionPlan?> FindPlanByPriceAsync(
        Guid planPriceId,
        CancellationToken cancellationToken)
        => context.SubscriptionPlans
            .Include(plan => plan.Prices)
            .Include(plan => plan.Entitlements)
            .FirstOrDefaultAsync(
                plan => plan.Prices.Any(price => price.Id == planPriceId),
                cancellationToken);

    public Task<bool> HasActiveSubscriptionAsync(
        Guid userId,
        Guid planId,
        DateTimeOffset asOf,
        CancellationToken cancellationToken)
        => (from subscription in context.Subscriptions
            join price in context.PlanPrices on subscription.PlanPriceId equals price.Id
            where subscription.SubscriberUserId == userId
                  && price.PlanId == planId
                  && subscription.Status == SubscriptionStatus.Active
                  && subscription.CurrentPeriodEnd > asOf
            select subscription.Id)
            .AnyAsync(cancellationToken);

    public void AddSubscription(Subscription subscription)
        => context.Subscriptions.Add(subscription);

    public void AddPayment(Payment payment) => context.Payments.Add(payment);

    public void AddCredit(CreditLedgerEntry credit) => context.CreditLedger.Add(credit);
}
