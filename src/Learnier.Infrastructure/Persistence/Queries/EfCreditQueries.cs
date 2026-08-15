using Learnier.Application.Common.Abstractions;
using Learnier.Application.Features.Billing.Queries;
using Microsoft.EntityFrameworkCore;

namespace Learnier.Infrastructure.Persistence.Queries;

/// <inheritdoc cref="ICreditQueries"/>
internal sealed class EfCreditQueries(AppDbContext context) : ICreditQueries
{
    public async Task<IReadOnlyList<CreditBalanceItem>> GetBalancesAsync(
        Guid learnerUserId,
        CancellationToken cancellationToken)
        // Bakiye gruplanip toplanarak bulunur; saklanan sayac yok.
        // Sifir bakiyeler de doner: "hakkin bitti" ile "hic hakkin yoktu" farkli.
        => await context.CreditLedger
            .AsNoTracking()
            .Where(e => e.LearnerUserId == learnerUserId)
            .Where(e => context.Subscriptions.Any(s => s.Id == e.SubscriptionId))
            .GroupBy(e => new { e.SubscriptionId, e.SessionType })
            .Select(g => new CreditBalanceItem(
                g.Key.SubscriptionId,
                g.Key.SessionType,
                g.Sum(e => e.Quantity)))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<CreditLedgerItem>> ListEntriesAsync(
        Guid subscriptionId,
        Guid learnerUserId,
        CancellationToken cancellationToken)
        => await context.CreditLedger
            .AsNoTracking()
            .Where(e => e.SubscriptionId == subscriptionId && e.LearnerUserId == learnerUserId)
            .Where(e => context.Subscriptions.Any(s => s.Id == e.SubscriptionId))
            // Eskiden yeniye: defterin okunma sirasi bu.
            .OrderBy(e => e.CreatedAt)
            .ThenBy(e => e.Id)
            .Select(e => new CreditLedgerItem(
                e.Id,
                e.SessionType,
                e.Quantity,
                e.TransactionType,
                e.BookingId,
                e.ExpiresAt,
                e.CreatedAt))
            .ToListAsync(cancellationToken);
}
