using Learnier.Application.Common.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Learnier.Infrastructure.Persistence.Queries;

/// <inheritdoc cref="ICreditLedgerQueries"/>
internal sealed class EfCreditLedgerQueries(AppDbContext context) : ICreditLedgerQueries
{
    public async Task<IReadOnlyList<CreditLedgerItem>> ListForLearnerAsync(
        Guid learnerUserId,
        Guid? subscriptionId,
        CancellationToken cancellationToken)
    {
        var query = context.CreditLedger
            .AsNoTracking()
            .Where(e => e.LearnerUserId == learnerUserId)
            // Abonelik kiraci filtresine tabi; baska kurumun defteri gorunmez.
            .Where(e => context.Subscriptions.Any(s => s.Id == e.SubscriptionId));

        if (subscriptionId is { } filter)
        {
            query = query.Where(e => e.SubscriptionId == filter);
        }

        var entries = await query
            // Eskiden yeniye: defterin okunma sirasi bu.
            .OrderBy(e => e.CreatedAt)
            .ThenBy(e => e.Id)
            .Select(e => new
            {
                e.Id,
                e.SubscriptionId,
                e.SessionType,
                e.Quantity,
                e.TransactionType,
                e.BookingId,
                e.ExpiresAt,
                e.CreatedAt
            })
            .ToListAsync(cancellationToken);

        // Yuruyen bakiye bellekte hesaplanir: abonelik ve ders turu basina ayri
        // birikir, cunku bakiye de o kirilimda tutulur.
        var running = new Dictionary<(Guid, Domain.Scheduling.SessionType), int>();
        var items = new List<CreditLedgerItem>(entries.Count);

        foreach (var e in entries)
        {
            var key = (e.SubscriptionId, e.SessionType);
            running[key] = running.GetValueOrDefault(key) + e.Quantity;

            items.Add(new CreditLedgerItem(
                e.Id,
                e.SubscriptionId,
                e.SessionType,
                e.Quantity,
                running[key],
                e.TransactionType,
                e.BookingId,
                e.ExpiresAt,
                e.CreatedAt));
        }

        return items;
    }
}
