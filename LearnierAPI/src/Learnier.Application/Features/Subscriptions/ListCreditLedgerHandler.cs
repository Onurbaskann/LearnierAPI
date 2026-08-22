using Learnier.Application.Common.Abstractions;
using Learnier.Application.Common.Results;

namespace Learnier.Application.Features.Subscriptions;

/// <summary>
/// Ders hakki defterinin hareket gecmisi.
/// </summary>
/// <remarks>
/// Ogrenci kendi defterini gorur; baskasininkini gormek icin yetki gerekir.
/// Yetkisi olmayan reddedilmez, yalnizca kendi kayitlariyla sinirli kalir -
/// bu yuzden karar caginin tarafinda verilip <c>canViewOthers</c> ile bildirilir.
/// </remarks>
public sealed class ListCreditLedgerHandler(
    ICreditLedgerQueries queries,
    ICurrentUser currentUser,
    ICurrentTenant currentTenant)
{
    public async Task<Result<IReadOnlyList<CreditLedgerItem>>> Handle(
        Guid? learnerUserId,
        Guid? subscriptionId,
        bool canViewOthers,
        CancellationToken cancellationToken)
    {
        if (!currentTenant.HasTenant)
        {
            return Error.Forbidden("tenant.organization_required");
        }

        if (currentUser.UserId is not { } actingUserId)
        {
            return Error.Unauthorized("common.unauthorized");
        }

        var target = learnerUserId ?? actingUserId;

        if (target != actingUserId && !canViewOthers)
        {
            return Error.Forbidden("subscriptions.ledger_not_owned");
        }

        var entries = await queries.ListForLearnerAsync(target, subscriptionId, cancellationToken);

        return Result.Success(entries);
    }
}
