namespace Learnier.Domain.Common;

/// <summary>
/// Domain icinde gerceklesmis, geri alinamaz bir olay.
/// </summary>
/// <remarks>
/// Mediator kutuphanesi kullanmadigimiz icin olaylarin yayini
/// <c>DomainEventDispatchInterceptor</c> tarafindan <c>SaveChanges</c> sirasinda yapilir.
/// Bunun onemi: yan etkiler ana degisiklikle ayni transaction icinde islenir,
/// yani "rezervasyon kaydedildi ama bildirim gitmedi" durumu olusamaz.
/// </remarks>
public abstract record DomainEvent
{
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
}
