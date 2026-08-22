namespace Learnier.Domain.Common;

/// <summary>
/// Tutarlilik sinirini temsil eden varlik: domain olayi uretebilir.
/// Bir islem icinde yalnizca tek bir aggregate degistirilmesi hedeflenir.
/// </summary>
public abstract class AggregateRoot : Entity
{
    private readonly List<DomainEvent> _domainEvents = [];

    /// <summary>
    /// Henuz yayinlanmamis olaylar. Yayin sonrasi <see cref="ClearDomainEvents"/> ile temizlenir.
    /// </summary>
    public IReadOnlyCollection<DomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected void RaiseDomainEvent(DomainEvent domainEvent) => _domainEvents.Add(domainEvent);

    public void ClearDomainEvents() => _domainEvents.Clear();
}
