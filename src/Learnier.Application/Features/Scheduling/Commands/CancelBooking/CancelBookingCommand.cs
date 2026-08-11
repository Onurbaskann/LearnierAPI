using Learnier.Application.Common.Abstractions;
using Learnier.Application.Common.Results;
using Learnier.Domain.Scheduling;

namespace Learnier.Application.Features.Scheduling.Commands.CancelBooking;

public sealed record CancelBookingCommand(Guid BookingId, string? Reason = null);

/// <param name="Refunded">
/// Iptal, ucretsiz iptal sinirindan once yapildiysa dogru. Yanlissa hak yanar.
/// </param>
/// <param name="PromotedBookingId">
/// Bosalan yere bekleme listesinden alinan rezervasyon; yoksa bos.
/// </param>
public sealed record CancelBookingResult(bool Refunded, Guid? PromotedBookingId);

/// <summary>
/// Rezervasyonu iptal eder ve bosalan yeri bekleme listesinden doldurur.
/// </summary>
/// <remarks>
/// <para>
/// Iptal de rezervasyon kadar yaris kosuluna aciktir: iki es zamanli iptal, ayni
/// bekleme listesi kaydini iki kez yukseltebilir veya bosalan tek yere iki kisi
/// alinabilir. Bu yuzden akis yine islem + oturum satiri kilidi altinda calisir.
/// </para>
/// <para>
/// Hak iadesi <see cref="IBookingEntitlementPolicy"/>'ye birakilir. Faz 3'te iade
/// bir sey yapmaz; kredi defteri Faz 4'te baglanacak.
/// </para>
/// </remarks>
public sealed class CancelBookingHandler(
    ISchedulingRepository scheduling,
    IBookingEntitlementPolicy entitlements,
    ICurrentUser currentUser,
    ICurrentTenant currentTenant,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    public async Task<Result<CancelBookingResult>> Handle(
        CancelBookingCommand command,
        bool canManageAllBookings,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!currentTenant.HasTenant)
        {
            return SchedulingErrors.OrganizationContextRequired;
        }

        if (currentUser.UserId is not { } actingUserId)
        {
            return Error.Unauthorized("common.unauthorized");
        }

        var now = clock.UtcNow;

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);

        var booking = await scheduling.FindBookingAsync(command.BookingId, cancellationToken);

        if (booking is null || booking.Status is BookingStatus.Cancelled)
        {
            return SchedulingErrors.BookingNotFound;
        }

        // Kendi rezervasyonu degilse yetki gerekir.
        if (booking.LearnerUserId != actingUserId && !canManageAllBookings)
        {
            return SchedulingErrors.BookingNotOwned;
        }

        // Kilit, bosalan yerin yukseltilmesi icin gerekli: yukseltme oturumun
        // kontenjanini okuyup yaziyor.
        var session = await scheduling.FindSessionForUpdateAsync(booking.SessionId, cancellationToken);

        if (session is null)
        {
            return SchedulingErrors.SessionNotFound;
        }

        if (now >= session.StartsAt)
        {
            return SchedulingErrors.SessionAlreadyStarted;
        }

        // Ucretsiz iptal siniri tanimli degilse iade hep yapilir; tanimliysa
        // sinirdan once iptal edenlere.
        var refundable = session.CancellationDeadlineAt is not { } deadline || now <= deadline;

        var wasReserved = booking.Status is BookingStatus.Reserved;

        booking.Cancel(now, command.Reason);

        await entitlements.ReleaseAsync(booking, refundable, cancellationToken);

        // Yalnizca dolu bir koltuk bosaldiysa yukseltme yapilir; bekleme
        // listesindeki bir kaydin iptali kontenjani degistirmez.
        Guid? promotedBookingId = null;

        if (wasReserved)
        {
            var next = await scheduling.FindNextWaitlistedAsync(session.Id, cancellationToken);

            if (next is not null)
            {
                next.Promote();
                promotedBookingId = next.Id;
            }
            else if (session.SessionType is SessionType.Private)
            {
                // Birebir slot egitmen tarafindan tekil olarak acilir. Rezervasyon
                // iptal edilince ayni oturum tekrar acilmaz; egitmen isterse yeni
                // bir slot acar.
                session.Cancel("Rezervasyon iptal edildi.");
            }
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new CancelBookingResult(refundable, promotedBookingId);
    }
}
