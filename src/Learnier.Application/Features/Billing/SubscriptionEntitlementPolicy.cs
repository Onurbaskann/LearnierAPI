using Learnier.Application.Common.Abstractions;
using Learnier.Application.Common.Results;
using Learnier.Domain.Billing;
using Learnier.Domain.Scheduling;

namespace Learnier.Application.Features.Billing;

/// <summary>
/// Rezervasyon hakkini aboneliklere ve kredi defterine gore belirler.
/// </summary>
/// <remarks>
/// <para>
/// Faz 3'te birakilan yer tutucunun yerini alir. Kaynak dokumanin 1. bolumundeki
/// ayrim korunuyor: rezervasyon akisi bu tipin ne yaptigini bilmez, yalnizca
/// "izin var mi, hangi hakla" sorusunun yanitini alir.
/// </para>
/// <para>
/// Application katmaninda duruyor cunku icerigi bir <b>is kurali</b>: yalnizca
/// soyutlamalara (<see cref="IBillingRepository"/>, <see cref="IClock"/>) bagimli,
/// hicbir altyapi detayi tasimiyor.
/// </para>
/// <para>
/// Sira: aktif abonelikler bulunur, plani egitimi kapsayan ve oturum turune hak
/// taniyan ilk abonelik secilir. Sinirsiz erisimde defter hareketi uretilmez;
/// sayili hakta bakiye kilit altinda okunur.
/// </para>
/// </remarks>
public sealed class SubscriptionEntitlementPolicy(
    IBillingRepository billing,
    IClock clock)
    : IBookingEntitlementPolicy
{
    public async Task<Result<BookingGrant>> AuthorizeAsync(
        Guid learnerUserId,
        LessonSession session,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);

        var now = clock.UtcNow;

        var subscriptions = await billing.FindActiveSubscriptionsForLearnerAsync(
            learnerUserId, now, cancellationToken);

        if (subscriptions.Count is 0)
        {
            return BillingErrors.NoActiveSubscription;
        }

        // Bir ogrencinin birden fazla aboneligi olabilir. Eski tarihliden
        // baslanir: once alinan hak once tukensin.
        var courseCovered = false;

        foreach (var subscription in subscriptions.OrderBy(s => s.StartsAt))
        {
            var plan = await billing.FindPlanAsync(
                subscription.PlanPrice.PlanId, includeDetails: true, cancellationToken);

            if (plan is null)
            {
                continue;
            }

            if (!await billing.PlanCoversCourseAsync(plan.Id, session.CourseId, cancellationToken))
            {
                continue;
            }

            courseCovered = true;

            var entitlement = plan.Entitlements
                .FirstOrDefault(e => e.SessionType == session.SessionType);

            if (entitlement is null)
            {
                continue;
            }

            if (entitlement.EntitlementType is EntitlementType.BookingAccess)
            {
                // Sinirsiz erisim: defter hareketi uretilmez, plan erisiminin
                // gecerli olmasi yeterli (kaynak dokuman 9. bolum).
                return Result.Success(
                    new BookingGrant(BookingAccessSource.Subscription, subscription.Id));
            }

            // Sayili hak: bakiye kilit altinda okunur. Aksi halde es zamanli iki
            // rezervasyon ayni son krediyi harcayip bakiyeyi eksiye dusurebilirdi.
            var balance = await billing.GetCreditBalanceForUpdateAsync(
                subscription.Id, learnerUserId, session.SessionType, cancellationToken);

            if (balance <= 0)
            {
                continue;
            }

            return Result.Success(
                new BookingGrant(BookingAccessSource.Credit, subscription.Id));
        }

        // Kapsam vardi ama hak yoktu: bakiye bitmis veya oturum turu kapsam disi.
        return courseCovered
            ? BillingErrors.InsufficientCredit
            : BillingErrors.CourseNotCovered;
    }

    public Task ConsumeAsync(
        SessionBooking booking,
        LessonSession session,
        BookingGrant grant,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(booking);
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(grant);

        // Sinirsiz erisimde harcanacak hak yok.
        if (grant.AccessSource is not BookingAccessSource.Credit
            || grant.SubscriptionId is not { } subscriptionId)
        {
            return Task.CompletedTask;
        }

        // Oturum disaridan geliyor: rezervasyon yeni olusturuldugu icin
        // booking.Session navigasyonu yuklu degil.
        billing.AddLedgerEntry(CreditLedgerEntry.Consume(
            subscriptionId,
            booking.LearnerUserId,
            session.SessionType,
            booking.Id,
            clock.UtcNow));

        return Task.CompletedTask;
    }

    public async Task ReleaseAsync(
        SessionBooking booking,
        bool refundable,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(booking);

        // Yalnizca kredi ile yapilan rezervasyonda iade edilecek hak var;
        // sinirsiz erisimde harcanan bir sey yok.
        if (booking.AccessSource is not BookingAccessSource.Credit
            || booking.SubscriptionId is not { } subscriptionId)
        {
            return;
        }

        // Ucretsiz iptal suresi gectiyse hak yanar.
        if (!refundable)
        {
            return;
        }

        var usage = await billing.FindUsageEntryAsync(booking.Id, cancellationToken);

        if (usage is null)
        {
            return;
        }

        // Harcama hareketi silinmez veya duzeltilmez; ters yonlu yeni hareket
        // yazilir. Boylece defterde ne olduysa oldugu gibi durur.
        billing.AddLedgerEntry(CreditLedgerEntry.Refund(
            subscriptionId,
            booking.LearnerUserId,
            usage.SessionType,
            booking.Id,
            clock.UtcNow,
            Math.Abs(usage.Quantity)));
    }
}
