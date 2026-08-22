using Learnier.Application.Common.Abstractions;
using Learnier.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Learnier.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Biriken domain olaylarini kaydetme islemiyle ayni transaction icinde yayinlar.
/// </summary>
/// <remarks>
/// Olaylar <c>SaveChanges</c> tamamlandiktan <b>sonra</b>, ama transaction commit
/// edilmeden once yayinlanir. Boylece yan etkiler ana degisiklikle atomik olur:
/// "rezervasyon kaydedildi ama kontenjan projeksiyonu guncellenmedi" durumu olusamaz.
/// </remarks>
internal sealed class DomainEventDispatchInterceptor(IDomainEventDispatcher dispatcher)
    : SaveChangesInterceptor
{
    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        await DispatchDomainEvents(eventData.Context, cancellationToken);
        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    private async Task DispatchDomainEvents(DbContext? context, CancellationToken cancellationToken)
    {
        if (context is null)
        {
            return;
        }

        var aggregates = context.ChangeTracker
            .Entries<AggregateRoot>()
            .Where(entry => entry.Entity.DomainEvents.Count > 0)
            .Select(entry => entry.Entity)
            .ToList();

        if (aggregates.Count == 0)
        {
            return;
        }

        var domainEvents = aggregates.SelectMany(a => a.DomainEvents).ToList();

        // Handler'lar yeni olay uretirse sonsuz donguye girmemek icin once temizlenir.
        foreach (var aggregate in aggregates)
        {
            aggregate.ClearDomainEvents();
        }

        await dispatcher.Dispatch(domainEvents, cancellationToken);
    }
}
