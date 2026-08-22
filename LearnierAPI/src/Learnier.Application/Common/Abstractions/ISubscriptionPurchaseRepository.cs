using Learnier.Domain.Billing;

namespace Learnier.Application.Common.Abstractions;

/// <summary>
/// Ogrencinin katalogdan plan satin alma yolu.
/// </summary>
/// <remarks>
/// <see cref="IPackagePurchaseRepository"/> demo akisina aittir: plani adiyla arar
/// ve gerekirse uretir. Burasi tersini yapar - plan zaten yoneticinin actigi bir
/// kayittir, yalnizca fiyat surumu uzerinden bulunur. Ikisini ayirmak, gercek satin
/// almanin plan uretme yeteneginden uzak kalmasini saglar.
/// </remarks>
public interface ISubscriptionPurchaseRepository
{
    /// <summary>
    /// Fiyat surumunun bagli oldugu plani fiyatlari ve hak tanimlariyla getirir.
    /// </summary>
    /// <remarks>
    /// Kiraci filtresi geregi baska kurumun plani donmez; cagiran taraf ayrica
    /// organizasyon karsilastirmasi yapmak zorunda kalmaz.
    /// </remarks>
    Task<SubscriptionPlan?> FindPlanByPriceAsync(
        Guid planPriceId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Kullanicinin ayni plandan suregelen bir aboneligi var mi.
    /// </summary>
    Task<bool> HasActiveSubscriptionAsync(
        Guid userId,
        Guid planId,
        DateTimeOffset asOf,
        CancellationToken cancellationToken);

    void AddSubscription(Subscription subscription);

    void AddPayment(Payment payment);

    void AddCredit(CreditLedgerEntry credit);
}
