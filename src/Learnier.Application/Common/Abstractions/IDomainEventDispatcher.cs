using Learnier.Domain.Common;

namespace Learnier.Application.Common.Abstractions;

/// <summary>
/// Domain olaylarini kayitli handler'lara ulastirir.
/// </summary>
/// <remarks>
/// Mediator kutuphanesi yerine bu kucuk soyutlama kullaniliyor: tek ihtiyacimiz
/// "bir olay, birden cok yan etki" senaryosu ve bu ~40 satirlik bir islev.
/// </remarks>
public interface IDomainEventDispatcher
{
    Task Dispatch(IReadOnlyCollection<DomainEvent> domainEvents, CancellationToken cancellationToken);
}
