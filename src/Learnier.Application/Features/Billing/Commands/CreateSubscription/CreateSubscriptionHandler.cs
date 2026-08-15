using Learnier.Application.Common.Abstractions;
using Learnier.Application.Common.Results;
using Learnier.Domain.Billing;

namespace Learnier.Application.Features.Billing.Commands.CreateSubscription;

/// <summary>
/// Abonelik acar ve ilk donemin ders haklarini deftere yazar.
/// </summary>
/// <remarks>
/// <para>
/// Abonelik <c>Active</c> baslar. Odeme entegrasyonu Faz 5'te baglandiginda
/// <c>Pending</c> ile baslayip odeme onayinda aktiflesecek.
/// </para>
/// <para>
/// Sayili haklar deftere <c>PeriodGrant</c> olarak yazilir. Sinirsiz erisim icin
/// hareket uretilmez; kaynak dokumanin 9. bolumu bunu ozellikle belirtiyor -
/// erisimin gecerli olmasi yeterli.
/// </para>
/// </remarks>
public sealed class CreateSubscriptionHandler(
    IBillingRepository billing,
    IUserRepository users,
    ICurrentTenant currentTenant,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    public async Task<Result<CreateSubscriptionResult>> Handle(
        CreateSubscriptionCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (currentTenant.OrganizationId is not { } organizationId)
        {
            return BillingErrors.OrganizationContextRequired;
        }

        var price = await billing.FindPlanPriceAsync(command.PlanPriceId, cancellationToken);

        if (price is null)
        {
            return BillingErrors.PlanPriceNotFound;
        }

        // Arsivlenmis fiyattan yeni abonelik satilamaz; eski abonelikler devam eder.
        if (price.Status is not PlanPriceStatus.Active)
        {
            return BillingErrors.PlanPriceNotActive;
        }

        var plan = await billing.FindPlanAsync(price.PlanId, includeDetails: true, cancellationToken);

        if (plan is null)
        {
            return BillingErrors.PlanNotFound;
        }

        if (plan.Status is not PlanStatus.Active)
        {
            return BillingErrors.PlanNotActive;
        }

        var now = clock.UtcNow;
        var periodEnd = NextPeriodEnd(now, price.BillingInterval, price.BillingIntervalCount);

        Subscription subscription;

        if (command.SubscriberUserId is { } subscriberUserId)
        {
            var subscriber = await users.FindByIdAsync(subscriberUserId, cancellationToken);

            if (subscriber is null)
            {
                return BillingErrors.LearnerNotFound;
            }

            subscription = Subscription.CreateForUser(
                organizationId, subscriber.Id, price.Id, now, periodEnd);
        }
        else
        {
            subscription = Subscription.CreateForOrganization(
                organizationId, command.SubscriberOrganizationId!.Value, price.Id, now, periodEnd);
        }

        subscription.Activate();
        billing.AddSubscription(subscription);

        // Donem haklari yalnizca bireysel abonelikte simdi yazilir: kurumsal
        // abonelikte hak, koltuk atanan calisana ait olur.
        var grantedCredits = 0;

        if (command.SubscriberUserId is { } learnerUserId)
        {
            grantedCredits = GrantPeriodCredits(
                billing, plan, subscription.Id, learnerUserId, now, periodEnd);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new CreateSubscriptionResult(
            subscription.Id,
            subscription.Status,
            subscription.CurrentPeriodEnd,
            grantedCredits);
    }

    /// <summary>
    /// Planin sayili haklarini deftere alacak olarak yazar.
    /// </summary>
    /// <returns>Yazilan hareket sayisi.</returns>
    internal static int GrantPeriodCredits(
        IBillingRepository billing,
        SubscriptionPlan plan,
        Guid subscriptionId,
        Guid learnerUserId,
        DateTimeOffset now,
        DateTimeOffset periodEnd)
    {
        var granted = 0;

        foreach (var entitlement in plan.Entitlements)
        {
            // Sinirsiz erisim defterde yer tutmaz.
            if (entitlement.EntitlementType is not EntitlementType.LessonCredit
                || entitlement.Quantity is not { } quantity)
            {
                continue;
            }

            // Donem sonunda kullanilmayan hak duser; abonelik boyunca gecerli
            // olan haklarda sure sinirsizdir.
            var expiresAt = entitlement.ResetPeriod is EntitlementResetPeriod.Subscription
                ? (DateTimeOffset?)null
                : periodEnd;

            billing.AddLedgerEntry(CreditLedgerEntry.Grant(
                subscriptionId,
                learnerUserId,
                entitlement.SessionType,
                quantity,
                now,
                expiresAt));

            granted++;
        }

        return granted;
    }

    /// <summary>
    /// Fatura araligina gore donem bitisini hesaplar.
    /// </summary>
    private static DateTimeOffset NextPeriodEnd(
        DateTimeOffset from,
        BillingInterval interval,
        int intervalCount)
        => interval switch
        {
            BillingInterval.Month => from.AddMonths(intervalCount),
            BillingInterval.Year => from.AddYears(intervalCount),
            _ => from.AddMonths(intervalCount)
        };
}
