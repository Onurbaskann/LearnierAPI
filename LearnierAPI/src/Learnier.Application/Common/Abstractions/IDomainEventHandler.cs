using Learnier.Domain.Common;

namespace Learnier.Application.Common.Abstractions;

/// <summary>
/// Belirli bir domain olayina tepki veren islem.
/// </summary>
/// <remarks>
/// Ayni olay icin birden fazla handler kaydedilebilir; hepsi calistirilir.
/// Handler'lar <c>SaveChanges</c> ile ayni transaction icinde calisir, bu yuzden
/// icinde uzun suren veya disariya cikan is (e-posta gonderme gibi) yapilmamali;
/// oyle isler bir outbox kaydi olusturup ayrica islenmeli.
/// </remarks>
public interface IDomainEventHandler<in TEvent>
    where TEvent : DomainEvent
{
    Task Handle(TEvent domainEvent, CancellationToken cancellationToken);
}
