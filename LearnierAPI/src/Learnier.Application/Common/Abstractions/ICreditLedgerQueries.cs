using Learnier.Domain.Billing;
using Learnier.Domain.Scheduling;

namespace Learnier.Application.Common.Abstractions;

/// <param name="Quantity">Alacak icin pozitif, harcama icin negatif.</param>
/// <param name="RunningBalance">Bu hareketten sonraki bakiye.</param>
public sealed record CreditLedgerItem(
    Guid Id,
    Guid SubscriptionId,
    SessionType SessionType,
    int Quantity,
    int RunningBalance,
    CreditTransactionType TransactionType,
    Guid? BookingId,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset CreatedAt);

/// <summary>
/// Ders hakki defterinin hareket gecmisi.
/// </summary>
/// <remarks>
/// <c>ActivePackageAccess.RemainingCredits</c> bakiyenin <b>sonucunu</b> verir;
/// buradaki sorgu o sonucun nasil olustugunu gosterir. Destek tarafinda
/// "hakkim neden bu kadar" sorusunun tek yaniti defterin kendisidir.
/// </remarks>
public interface ICreditLedgerQueries
{
    Task<IReadOnlyList<CreditLedgerItem>> ListForLearnerAsync(
        Guid learnerUserId,
        Guid? subscriptionId,
        CancellationToken cancellationToken);
}
