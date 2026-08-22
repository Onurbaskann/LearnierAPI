using Learnier.Application.Common.Abstractions;
using Learnier.Application.Common.Results;
using Learnier.Application.Features.Billing;
using Learnier.Domain.Billing;
using Learnier.Domain.Scheduling;

namespace Learnier.Application.Features.Subscriptions.Commands.CreateSubscription;

/// <param name="PlanPriceId">
/// Satin alinan fiyat surumu. Abonelik plana degil fiyata baglanir; boylece plan
/// sonradan zamlansa da bu aboneligin tutari degismez.
/// </param>
public sealed record CreateSubscriptionCommand(Guid PlanPriceId);

public sealed record GrantedCreditItem(
    SessionType SessionType,
    int Quantity,
    int? LessonDurationMinutes,
    DateTimeOffset? ExpiresAt);

public sealed record CreateSubscriptionResult(
    Guid SubscriptionId,
    Guid PlanId,
    string PlanName,
    Guid? PaymentId,
    DateTimeOffset CurrentPeriodStart,
    DateTimeOffset CurrentPeriodEnd,
    IReadOnlyList<GrantedCreditItem> GrantedCredits);

/// <summary>
/// Katalogdaki bir plani gercek abonelige cevirir.
/// </summary>
/// <remarks>
/// <para>
/// <c>demo-purchases</c> ucundan farki: burada plan uretilmez. Yonetici plani
/// olusturur, fiyatlandirir, hak tanimlarini yazar ve satisa acar; ogrenci yalnizca
/// hazir bir fiyat surumunu secer.
/// </para>
/// <para>
/// Odeme saglayicisi henuz bagli olmadigi icin <see cref="Payment"/> kaydi
/// <c>manual</c> saglayicisiyla ve basarili olarak yazilir. Saglayici baglandiginda
/// degisecek yer burasidir: kayit <c>Pending</c> acilir, abonelik webhook ile
/// aktiflesir. Kaydin simdiden yazilmasi, o gecise abonelik-odeme baginin hazir
/// olmasini saglar.
/// </para>
/// </remarks>
public sealed class CreateSubscriptionHandler(
    ISubscriptionPurchaseRepository repository,
    ICurrentUser currentUser,
    ICurrentTenant currentTenant,
    IClock clock,
    IUnitOfWork unitOfWork)
{
    /// <summary>Odeme saglayicisi baglanana kadar kullanilan kayit kaynagi.</summary>
    private const string ManualPaymentProvider = "manual";

    public async Task<Result<CreateSubscriptionResult>> Handle(
        CreateSubscriptionCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (currentUser.UserId is not { } userId)
        {
            return Error.Unauthorized("common.unauthorized");
        }

        if (!currentTenant.HasTenant)
        {
            return BillingErrors.OrganizationContextRequired;
        }

        // Kiraci filtresi baska kurumun planini zaten gizler; ayrica organizasyon
        // karsilastirmasi yapmaya gerek yok.
        var plan = await repository.FindPlanByPriceAsync(command.PlanPriceId, cancellationToken);
        if (plan is null)
        {
            return BillingErrors.PlanPriceNotFound;
        }

        var price = plan.Prices.Single(item => item.Id == command.PlanPriceId);

        if (price.Status is not PlanPriceStatus.Active)
        {
            return BillingErrors.PlanPriceNotActive;
        }

        if (plan.Status is not PlanStatus.Active)
        {
            return BillingErrors.PlanNotActive;
        }

        // Demo satin almanin yan urunu olan planlar kataloga girmez; kimligi elle
        // yazilarak da satin alinamamalidir.
        if (plan.IsSystemGenerated)
        {
            return BillingErrors.PlanNotPurchasable;
        }

        if (plan.Entitlements.Count is 0)
        {
            return BillingErrors.PlanHasNoEntitlement;
        }

        var now = clock.UtcNow;

        if (await repository.HasActiveSubscriptionAsync(userId, plan.Id, now, cancellationToken))
        {
            return BillingErrors.AlreadySubscribed;
        }

        var periodEnd = AddBillingPeriod(now, price.BillingInterval, price.BillingIntervalCount);

        var subscription = Subscription.CreateForUser(
            plan.OrganizationId,
            userId,
            price.Id,
            now,
            periodEnd);
        subscription.Activate();
        repository.AddSubscription(subscription);

        Payment? payment = null;

        // Ucretsiz plan satilabilir ama sifir tutarli odeme kaydi anlamsizdir.
        if (price.Amount > 0)
        {
            payment = Payment.Create(
                price.Amount,
                price.Currency,
                ManualPaymentProvider,
                subscription.Id,
                userId);
            payment.MarkSucceeded($"{ManualPaymentProvider}-{subscription.Id:N}", now);
            repository.AddPayment(payment);
        }

        var grants = new List<GrantedCreditItem>();

        foreach (var entitlement in plan.Entitlements)
        {
            // Sinirsiz erisimde defter hareketi uretilmez: plan erisiminin gecerli
            // olmasi yeterli, sayilacak bir hak yok.
            if (entitlement.EntitlementType is not EntitlementType.LessonCredit
                || entitlement.Quantity is not { } quantity)
            {
                continue;
            }

            var expiresAt = CreditPeriodEnd(now, entitlement.ResetPeriod, periodEnd);

            repository.AddCredit(CreditLedgerEntry.Grant(
                subscription.Id,
                userId,
                entitlement.SessionType,
                quantity,
                now,
                expiresAt,
                now));

            grants.Add(new GrantedCreditItem(
                entitlement.SessionType,
                quantity,
                entitlement.LessonDurationMinutes,
                expiresAt));
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new CreateSubscriptionResult(
            subscription.Id,
            plan.Id,
            plan.Name,
            payment?.Id,
            subscription.CurrentPeriodStart,
            subscription.CurrentPeriodEnd,
            grants);
    }

    private static DateTimeOffset AddBillingPeriod(
        DateTimeOffset start,
        BillingInterval interval,
        int intervalCount)
        => interval switch
        {
            BillingInterval.Year => start.AddYears(intervalCount),
            _ => start.AddMonths(intervalCount)
        };

    /// <summary>
    /// Ilk kredi doneminin bitisi. Abonelik bitisini asamaz: odenmemis bir donemin
    /// hakki simdiden verilmis gorunmemeli.
    /// </summary>
    private static DateTimeOffset CreditPeriodEnd(
        DateTimeOffset start,
        EntitlementResetPeriod resetPeriod,
        DateTimeOffset subscriptionEnd)
    {
        var end = resetPeriod switch
        {
            EntitlementResetPeriod.Week => start.AddDays(7),
            EntitlementResetPeriod.Month => start.AddMonths(1),
            EntitlementResetPeriod.Year => start.AddYears(1),
            _ => subscriptionEnd
        };

        return end > subscriptionEnd ? subscriptionEnd : end;
    }
}
