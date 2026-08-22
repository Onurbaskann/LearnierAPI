using System.Collections.Concurrent;
using System.Reflection;
using Learnier.Application.Common.Abstractions;
using Learnier.Domain.Common;
using Microsoft.Extensions.DependencyInjection;

namespace Learnier.Infrastructure.Events;

/// <summary>
/// Domain olaylarini DI'dan cozulen <see cref="IDomainEventHandler{TEvent}"/> ornekleri ile isler.
/// </summary>
/// <remarks>
/// Olayin calisma zamani tipinden generic handler arayuzune gecmek icin yansima gerekiyor.
/// Maliyeti tip basina bir kez odenir: cozulen metot <see cref="HandleMethods"/> icinde onbelleklenir.
/// </remarks>
internal sealed class DomainEventDispatcher(IServiceProvider serviceProvider) : IDomainEventDispatcher
{
    private static readonly ConcurrentDictionary<Type, DispatchPlan> HandleMethods = new();

    public async Task Dispatch(
        IReadOnlyCollection<DomainEvent> domainEvents,
        CancellationToken cancellationToken)
    {
        foreach (var domainEvent in domainEvents)
        {
            var plan = HandleMethods.GetOrAdd(domainEvent.GetType(), CreatePlan);

            var handlers = serviceProvider.GetServices(plan.HandlerType);

            foreach (var handler in handlers)
            {
                if (handler is null)
                {
                    continue;
                }

                await (Task)plan.HandleMethod.Invoke(handler, [domainEvent, cancellationToken])!;
            }
        }
    }

    private static DispatchPlan CreatePlan(Type eventType)
    {
        var handlerType = typeof(IDomainEventHandler<>).MakeGenericType(eventType);

        var handleMethod = handlerType.GetMethod(nameof(IDomainEventHandler<DomainEvent>.Handle))
            ?? throw new InvalidOperationException(
                $"{handlerType} uzerinde Handle metodu bulunamadi.");

        return new DispatchPlan(handlerType, handleMethod);
    }

    private sealed record DispatchPlan(Type HandlerType, MethodInfo HandleMethod);
}
