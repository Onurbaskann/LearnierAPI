using Learnier.Domain.Billing;
using Learnier.Domain.Scheduling;

namespace Learnier.Application.Common.Abstractions;

/// <summary>
/// Abonelik ve kredi defteri yazma islemleri.
/// </summary>
public interface IBillingRepository
{
    Task<SubscriptionPlan?> FindPlanAsync(Guid planId, bool includeDetails, CancellationToken cancellationToken);

    Task<PlanPrice?> FindPlanPriceAsync(Guid planPriceId, CancellationToken cancellationToken);

    Task<Subscription?> FindSubscriptionAsync(Guid subscriptionId, CancellationToken cancellationToken);

    /// <summary>
    /// Ogrencinin aktif aboneliklerini, plan ve hak tanimlariyla birlikte getirir.
    /// </summary>
    /// <remarks>
    /// Hem bireysel abonelikler hem de koltuk atanmis kurumsal abonelikler doner:
    /// ogrenci ikisinden biriyle erisim kazanmis olabilir.
    /// </remarks>
    Task<IReadOnlyList<Subscription>> FindActiveSubscriptionsForLearnerAsync(
        Guid learnerUserId,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    /// <summary>
    /// Planin verilen egitimi kapsayip kapsamadigi.
    /// </summary>
    /// <remarks>
    /// Kapsam <c>CatalogAccess.All</c> ise her egitim dahildir. Aksi halde egitimin
    /// kendisi veya alani plana acikca eklenmis olmalidir.
    /// </remarks>
    Task<bool> PlanCoversCourseAsync(Guid planId, Guid courseId, CancellationToken cancellationToken);

    /// <summary>
    /// Ders hakki bakiyesi: <c>SUM(quantity)</c>.
    /// </summary>
    /// <remarks>
    /// Hicbir yerde "kalan ders" alani tutulmaz; bakiye her zaman defterden
    /// yeniden hesaplanir (kaynak dokuman 9. bolum).
    /// </remarks>
    Task<int> GetCreditBalanceAsync(
        Guid subscriptionId,
        Guid learnerUserId,
        SessionType sessionType,
        CancellationToken cancellationToken);

    /// <summary>
    /// Bakiyeyi, aboneligin kredi hareketlerini kilitleyerek okur.
    /// </summary>
    /// <remarks>
    /// Es zamanli iki rezervasyon ayni son krediyi harcayip bakiyeyi eksiye
    /// dusurebilir. Rezervasyon zaten oturum satirini kilitliyor, ancak farkli
    /// oturumlara ayni anda yapilan rezervasyonlar o kilidi paylasmaz; bu yuzden
    /// abonelik satiri da kilitlenir.
    /// </remarks>
    Task<int> GetCreditBalanceForUpdateAsync(
        Guid subscriptionId,
        Guid learnerUserId,
        SessionType sessionType,
        CancellationToken cancellationToken);

    /// <summary>Rezervasyona bagli harcama hareketini bulur.</summary>
    Task<CreditLedgerEntry?> FindUsageEntryAsync(Guid bookingId, CancellationToken cancellationToken);

    void AddPlan(SubscriptionPlan plan);

    void AddSubjectAccess(PlanSubjectAccess access);

    void AddCourseAccess(PlanCourseAccess access);

    void AddSubscription(Subscription subscription);

    void AddLedgerEntry(CreditLedgerEntry entry);
}
