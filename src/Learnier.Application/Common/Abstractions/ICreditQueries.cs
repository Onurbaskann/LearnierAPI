using Learnier.Application.Features.Billing.Queries;

namespace Learnier.Application.Common.Abstractions;

/// <summary>
/// Kredi defteri okuma sorgulari.
/// </summary>
public interface ICreditQueries
{
    /// <summary>
    /// Ogrencinin abonelik ve ders turu basina bakiyeleri.
    /// </summary>
    /// <remarks>
    /// Bakiye <c>SUM(quantity)</c> ile hesaplanir; saklanan bir sayac yok.
    /// </remarks>
    Task<IReadOnlyList<CreditBalanceItem>> GetBalancesAsync(
        Guid learnerUserId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<CreditLedgerItem>> ListEntriesAsync(
        Guid subscriptionId,
        Guid learnerUserId,
        CancellationToken cancellationToken);
}
