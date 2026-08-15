using Learnier.Application.Common.Abstractions;
using Learnier.Domain.Billing;
using Learnier.Domain.Scheduling;
using Microsoft.EntityFrameworkCore;

namespace Learnier.Infrastructure.Persistence.Repositories;

/// <inheritdoc cref="IBillingRepository"/>
internal sealed class EfBillingRepository(AppDbContext context) : IBillingRepository
{
    public async Task<SubscriptionPlan?> FindPlanAsync(
        Guid planId,
        bool includeDetails,
        CancellationToken cancellationToken)
    {
        var query = context.SubscriptionPlans.AsQueryable();

        if (includeDetails)
        {
            query = query.Include(p => p.Prices).Include(p => p.Entitlements);
        }

        return await query.FirstOrDefaultAsync(p => p.Id == planId, cancellationToken);
    }

    // PlanPrice kendi organizasyon sutununu tasimaz; kiraci siniri plan uzerinden.
    public async Task<PlanPrice?> FindPlanPriceAsync(
        Guid planPriceId,
        CancellationToken cancellationToken)
        => await context.PlanPrices
            .Where(p => p.Id == planPriceId)
            .Where(p => context.SubscriptionPlans.Any(sp => sp.Id == p.PlanId))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<Subscription?> FindSubscriptionAsync(
        Guid subscriptionId,
        CancellationToken cancellationToken)
        => await context.Subscriptions
            .FirstOrDefaultAsync(s => s.Id == subscriptionId, cancellationToken);

    /// <inheritdoc />
    /// <remarks>
    /// Iki yoldan erisim degerlendirilir: ogrencinin kendi aboneligi veya koltuk
    /// atanmis kurumsal abonelik. Ikisi de <c>Active</c> ve donemi gecerli olmali.
    /// </remarks>
    public async Task<IReadOnlyList<Subscription>> FindActiveSubscriptionsForLearnerAsync(
        Guid learnerUserId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
        => await context.Subscriptions
            .Include(s => s.PlanPrice)
            .Where(s => s.Status == SubscriptionStatus.Active
                        && s.CurrentPeriodEnd > now)
            .Where(s =>
                s.SubscriberUserId == learnerUserId
                || context.SubscriptionSeats.Any(seat =>
                    seat.SubscriptionId == s.Id
                    && seat.RevokedAt == null
                    && context.Memberships.Any(m =>
                        m.Id == seat.MembershipId && m.UserId == learnerUserId)))
            .ToListAsync(cancellationToken);

    public async Task<bool> PlanCoversCourseAsync(
        Guid planId,
        Guid courseId,
        CancellationToken cancellationToken)
    {
        var plan = await context.SubscriptionPlans
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == planId, cancellationToken);

        if (plan is null)
        {
            return false;
        }

        // Tum katalog kapsamli planda ayrica erisim satiri aranmaz.
        if (plan.CatalogAccess is CatalogAccess.All)
        {
            return true;
        }

        // Egitim dogrudan eklenmis olabilir veya alani eklenmis olabilir.
        return await context.PlanCourseAccess
            .AnyAsync(a => a.PlanId == planId && a.CourseId == courseId, cancellationToken)
            || await context.PlanSubjectAccess
                .AnyAsync(
                    a => a.PlanId == planId
                         && context.Courses.Any(c => c.Id == courseId && c.SubjectId == a.SubjectId),
                    cancellationToken);
    }

    public async Task<int> GetCreditBalanceAsync(
        Guid subscriptionId,
        Guid learnerUserId,
        SessionType sessionType,
        CancellationToken cancellationToken)
        // Bakiye her zaman defterden hesaplanir; "kalan ders" alani yok.
        => await context.CreditLedger
            .Where(e => e.SubscriptionId == subscriptionId
                        && e.LearnerUserId == learnerUserId
                        && e.SessionType == sessionType)
            .SumAsync(e => e.Quantity, cancellationToken);

    /// <inheritdoc />
    /// <remarks>
    /// Abonelik satiri kilitlenir, sonra bakiye okunur. Bakiyenin kendisi bir satir
    /// olmadigi icin dogrudan kilitlenemez; abonelik uzerinden serilestirme yapilir.
    /// Boylece ayni aboneligin son kredisini iki istek birden harcayamaz.
    /// </remarks>
    public async Task<int> GetCreditBalanceForUpdateAsync(
        Guid subscriptionId,
        Guid learnerUserId,
        SessionType sessionType,
        CancellationToken cancellationToken)
    {
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT id FROM subscriptions WHERE id = {subscriptionId} FOR UPDATE",
            cancellationToken);

        return await GetCreditBalanceAsync(
            subscriptionId, learnerUserId, sessionType, cancellationToken);
    }

    public async Task<CreditLedgerEntry?> FindUsageEntryAsync(
        Guid bookingId,
        CancellationToken cancellationToken)
        => await context.CreditLedger
            .FirstOrDefaultAsync(
                e => e.BookingId == bookingId
                     && e.TransactionType == CreditTransactionType.BookingUsage,
                cancellationToken);

    public void AddPlan(SubscriptionPlan plan) => context.SubscriptionPlans.Add(plan);

    public void AddSubjectAccess(PlanSubjectAccess access) => context.PlanSubjectAccess.Add(access);

    public void AddCourseAccess(PlanCourseAccess access) => context.PlanCourseAccess.Add(access);

    public void AddSubscription(Subscription subscription) => context.Subscriptions.Add(subscription);

    public void AddLedgerEntry(CreditLedgerEntry entry) => context.CreditLedger.Add(entry);
}
