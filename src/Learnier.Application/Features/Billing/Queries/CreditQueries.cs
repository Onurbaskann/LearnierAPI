using Learnier.Application.Common.Abstractions;
using Learnier.Application.Common.Results;
using Learnier.Domain.Billing;
using Learnier.Domain.Scheduling;

namespace Learnier.Application.Features.Billing.Queries;

/// <param name="Balance">
/// Kalan hak: defterdeki hareketlerin toplami. Hicbir yerde saklanmaz,
/// her sorguda yeniden hesaplanir.
/// </param>
public sealed record CreditBalanceItem(Guid SubscriptionId, SessionType SessionType, int Balance);

public sealed record CreditLedgerItem(
    Guid Id,
    SessionType SessionType,
    int Quantity,
    CreditTransactionType TransactionType,
    Guid? BookingId,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset CreatedAt);

/// <summary>
/// Ogrencinin ders hakki bakiyeleri.
/// </summary>
/// <remarks>
/// Ogrenci kendi bakiyesini gorebilir; baskasininkini gormek icin yetki gerekir.
/// Karar cagiran tarafta verilir ve <c>canViewOthers</c> ile bildirilir.
/// </remarks>
public sealed class GetCreditBalanceHandler(
    ICreditQueries queries,
    ICurrentUser currentUser,
    ICurrentTenant currentTenant)
{
    public async Task<Result<IReadOnlyList<CreditBalanceItem>>> Handle(
        Guid? learnerUserId,
        bool canViewOthers,
        CancellationToken cancellationToken)
    {
        if (!currentTenant.HasTenant)
        {
            return BillingErrors.OrganizationContextRequired;
        }

        if (currentUser.UserId is not { } actingUserId)
        {
            return Error.Unauthorized("common.unauthorized");
        }

        var target = learnerUserId ?? actingUserId;

        if (target != actingUserId && !canViewOthers)
        {
            return Error.Forbidden("billing.balance_not_owned");
        }

        var balances = await queries.GetBalancesAsync(target, cancellationToken);

        return Result.Success(balances);
    }
}

/// <summary>
/// Bir aboneligin kredi hareketleri.
/// </summary>
/// <remarks>
/// Bakiyenin nasil olustugunu gostermek icin: her hareket ne zaman, hangi
/// sebeple yazildi. Defterin tek dogruluk kaynagi olmasinin pratik faydasi bu.
/// </remarks>
public sealed class ListCreditLedgerHandler(
    ICreditQueries queries,
    ICurrentTenant currentTenant)
{
    public async Task<Result<IReadOnlyList<CreditLedgerItem>>> Handle(
        Guid subscriptionId,
        Guid learnerUserId,
        CancellationToken cancellationToken)
    {
        if (!currentTenant.HasTenant)
        {
            return BillingErrors.OrganizationContextRequired;
        }

        var entries = await queries.ListEntriesAsync(subscriptionId, learnerUserId, cancellationToken);

        return Result.Success(entries);
    }
}
