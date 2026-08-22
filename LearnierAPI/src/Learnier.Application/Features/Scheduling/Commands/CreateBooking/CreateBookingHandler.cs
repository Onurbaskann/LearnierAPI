using Learnier.Application.Common.Abstractions;
using Learnier.Application.Common.Results;
using Learnier.Domain.Scheduling;

namespace Learnier.Application.Features.Scheduling.Commands.CreateBooking;

/// <summary>
/// Oturuma rezervasyon olusturur.
/// </summary>
/// <remarks>
/// <para>
/// <b>Kontenjan yarisi bu akisin merkezinde.</b> Kaynak dokumanin 7. bolumu
/// "once say, sonra insert et" yaklasiminin yetersiz oldugunu ozellikle vurguluyor:
/// es zamanli iki istek ayni anda "yer var" gorup kontenjani asabilir.
/// </para>
/// <para>
/// Koruma uc katmanli:
/// </para>
/// <list type="number">
/// <item>
/// Acik islem icinde oturum satiri <c>SELECT ... FOR UPDATE</c> ile kilitlenir;
/// ayni oturuma gelen ikinci istek bekler.
/// </item>
/// <item>
/// Kontenjan bellekteki koleksiyondan degil veritabanindan sayilir.
/// </item>
/// <item>
/// <c>session_bookings(session_id, learner_user_id)</c> UNIQUE kisiti ikinci
/// savunma hatti olarak durur.
/// </item>
/// </list>
/// <para>
/// Bu akis yuzunden <c>EnableRetryOnFailure</c> bilincli olarak kapali: yeniden
/// deneyen execution strategy elle baslatilan islemleri reddediyor.
/// </para>
/// </remarks>
public sealed class CreateBookingHandler(
    ISchedulingRepository scheduling,
    IBookingEntitlementPolicy entitlements,
    ICurrentUser currentUser,
    ICurrentTenant currentTenant,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    public async Task<Result<CreateBookingResult>> Handle(
        Guid sessionId,
        CreateBookingCommand command,
        bool canBookForOthers,
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

        var learnerUserId = command.LearnerUserId ?? actingUserId;

        // Baskasi adina rezervasyon yalnizca yetkiyle. Veli-ogrenci iliskisi
        // (learner_guardians) ileride burada ayrica degerlendirilecek.
        if (learnerUserId != actingUserId && !canBookForOthers)
        {
            return SchedulingErrors.BookingNotOwned;
        }

        var now = clock.UtcNow;

        // Islem burada aciliyor ve kilit alinana kadar hicbir yazma yapilmiyor.
        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);

        // Satir kilidi: bu noktadan sonra ayni oturuma gelen es zamanli istek,
        // bu islem bitene kadar bekler.
        var session = await scheduling.FindSessionForUpdateAsync(sessionId, cancellationToken);

        if (session is null)
        {
            return SchedulingErrors.SessionNotFound;
        }

        if (!session.IsBookable(now))
        {
            return SchedulingErrors.SessionNotBookable;
        }

        var currentDurationMinutes = (int)(session.EndsAt - session.StartsAt).TotalMinutes;
        var lessonDurationMinutes = command.LessonDurationMinutes ?? currentDurationMinutes;
        if (session.SessionType is SessionType.Private && lessonDurationMinutes is not (30 or 50))
        {
            return SchedulingErrors.LessonDurationInvalid;
        }

        var existing = await scheduling.FindActiveBookingAsync(
            session.Id, learnerUserId, cancellationToken);

        if (existing is not null)
        {
            return SchedulingErrors.AlreadyBooked;
        }

        var grant = await entitlements.AuthorizeAsync(
            learnerUserId,
            session,
            session.SessionType is SessionType.Private ? lessonDurationMinutes : null,
            cancellationToken);

        if (grant.IsFailure)
        {
            return grant.Error;
        }

        if (session.SessionType is SessionType.Private)
        {
            session.ApplyPrivateLessonDuration(lessonDurationMinutes);
        }

        // Kontenjan kilit altinda ve veritabanindan sayilir; bellekteki koleksiyon
        // eksik olabilir ve es zamanlilikta yaniltir.
        var reservedSeats = await scheduling.CountReservedSeatsAsync(session.Id, cancellationToken);

        // Birebir slot tek ogrenci icindir; doldugunda bekleme listesi olusmaz.
        // Bekleme listesi grup ve webinar oturumlarinda kullanilmaya devam eder.
        if (session.SessionType is SessionType.Private && reservedSeats >= session.Capacity)
        {
            return SchedulingErrors.SessionNotBookable;
        }

        var booking = session.Book(
            learnerUserId,
            actingUserId,
            grant.Value.AccessSource,
            now,
            reservedSeats,
            grant.Value.SubscriptionId);

        scheduling.AddBooking(booking);

        var creditReservation = await entitlements.ReserveAsync(booking, cancellationToken);
        if (creditReservation.IsFailure)
        {
            return creditReservation.Error;
        }

        // Asgari katilimci sarti saglandiysa oturum kesinlesir. Sayim veritabanindan
        // geliyor; bellekteki koleksiyon kilitli okumada yuklu degil.
        if (booking.Status is BookingStatus.Reserved)
        {
            session.Confirm(reservedSeats + 1);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        // Booking -> ledger ve ledger -> booking baglantilari ayni anda kurulursa
        // EF iki yeni satir arasinda ekleme sirasi belirleyemez. Once rezervasyon ve
        // Reserve hareketi yazilir, sonra rezervasyona hareket kimligi baglanir.
        // Iki kayit da ayni acik transaction icinde oldugu icin atomiklik korunur.
        if (creditReservation.Value is { } creditEntryId)
        {
            booking.AttachCreditEntry(creditEntryId);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);

        return new CreateBookingResult(booking.Id, booking.Status, booking.AccessSource);
    }
}
