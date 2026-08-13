using Learnier.Application.Common.Abstractions;
using Learnier.Application.Common.Results;
using Learnier.Domain.Scheduling;

namespace Learnier.Infrastructure.Scheduling;

/// <summary>
/// GECICI: her rezervasyona izin verir ve tek seferlik satin alma olarak isaretler.
/// </summary>
/// <remarks>
/// <para>
/// Abonelik, plan kapsami ve kredi defteri Faz 4'te yazilacak. O zamana kadar
/// rezervasyon akisinin calisabilmesi icin bu yer tutucu kullaniliyor: hak
/// kontrolu yapmaz, kredi dusmez, iade islemez.
/// </para>
/// <para>
/// <b>Uretimde tek basina kullanilmamali.</b> Bu haliyle her ogrenci her oturuma
/// bedelsiz rezervasyon yapabilir. Faz 4'te yerine abonelik kapsamini denetleyen
/// ve krediyi defterden dusen implementasyon gecmelidir.
/// </para>
/// </remarks>
internal sealed class DirectPurchaseEntitlementPolicy : IBookingEntitlementPolicy
{
    public Task<Result<BookingGrant>> AuthorizeAsync(
        Guid learnerUserId,
        LessonSession session,
        CancellationToken cancellationToken)
        => Task.FromResult(Result.Success(new BookingGrant(BookingAccessSource.DirectPurchase)));

    public Task<Result<Guid?>> ReserveAsync(
        SessionBooking booking,
        CancellationToken cancellationToken)
        => Task.FromResult(Result.Success<Guid?>(null));

    public Task<Result> ConsumeAsync(
        SessionBooking booking,
        CancellationToken cancellationToken)
        => Task.FromResult(Result.Success());

    public Task<Result<bool>> ReleaseAsync(
        SessionBooking booking,
        bool refundable,
        CancellationToken cancellationToken)
        => Task.FromResult(Result.Success(false));
}
