using Learnier.Application.Common.Abstractions;
using Learnier.Application.Common.Results;
using Learnier.Domain.Scheduling;
using Learnier.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Learnier.Infrastructure.Scheduling;

/// <summary>
/// Suresi dolmus, rezervasyonu olan oturumlari tamamlar.
/// </summary>
/// <remarks>
/// <para>
/// <b>Yoklama varsayimi:</b> otomatik tamamlamada kimse dersin nasil gectigini
/// bildirmedigi icin katilim <see cref="AttendanceStatus.Present"/> ve oturum
/// suresi kadar yazilir. <c>MarkedByUserId</c> bos birakilir; bu alan zaten
/// "otomatik isaretlemede bos kalir" seklinde tanimli, dolayisiyla elle girilen
/// yoklama ile otomatik olan kayitta ayirt edilebilir.
/// </para>
/// <para>
/// <b>Kiraci baglami:</b> arka plan kapsaminda organizasyon yoktur. Global
/// query filter kiraci yokken devre disi kaldigi icin (bkz.
/// <c>AppDbContext.ApplyTenantQueryFilters</c>) kredi ve hakedis servisleri
/// oldugu gibi kullanilabilir; is kurallari burada yeniden yazilmaz.
/// </para>
/// <para>
/// <b>Rezervasyonsuz slotlara dokunulmaz.</b> Kimsenin almadigi bir slot ders
/// degildir; tamamlanmasi yoklama ve hakedis uretmez, "Completed" isaretlemek
/// de yaniltici olur. Onlar bilincli olarak oldugu gibi birakilir.
/// </para>
/// </remarks>
internal sealed partial class SessionCompletionProcessor(
    AppDbContext context,
    ISchedulingRepository scheduling,
    IBookingEntitlementPolicy entitlements,
    IInstructorCompensationService compensation,
    IClock clock,
    ILogger<SessionCompletionProcessor> logger) : ISessionCompletionProcessor
{
    public async Task<SessionCompletionResult> ProcessDueAsync(
        int batchSize,
        TimeSpan gracePeriod,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(batchSize);

        var threshold = clock.UtcNow - gracePeriod;
        var candidateIds = await context.LessonSessions
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(session => session.EndsAt <= threshold)
            .Where(session => session.Status == LessonSessionStatus.Scheduled
                              || session.Status == LessonSessionStatus.Confirmed
                              || session.Status == LessonSessionStatus.InProgress)
            // Rezervasyonu olmayan slot tamamlanacak bir ders degildir.
            .Where(session => session.Bookings.Any(booking =>
                booking.Status == BookingStatus.Reserved
                || booking.Status == BookingStatus.Attended
                || booking.Status == BookingStatus.NoShow))
            .OrderBy(session => session.EndsAt)
            .ThenBy(session => session.Id)
            .Select(session => session.Id)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

        var completedSessions = 0;
        var completedBookings = 0;
        var skippedSessions = 0;

        foreach (var sessionId in candidateIds)
        {
            var bookingCount = await CompleteOneAsync(sessionId, cancellationToken);
            if (bookingCount is null)
            {
                skippedSessions++;
                continue;
            }

            completedSessions++;
            completedBookings += bookingCount.Value;
        }

        return new SessionCompletionResult(
            candidateIds.Count,
            completedSessions,
            completedBookings,
            skippedSessions);
    }

    /// <returns>
    /// Tamamlanan rezervasyon sayisi; oturum atlandiysa <see langword="null"/>.
    /// </returns>
    private async Task<int?> CompleteOneAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        await using var transaction = await context.BeginTransactionAsync(cancellationToken);

        // Ayni anda calisan ikinci bir worker ya da elle tamamlama istegi varsa
        // kilitli satir beklenmez; sonraki turda tekrar denenir.
        var session = await context.LessonSessions
            .FromSqlInterpolated($$"""
                SELECT *
                FROM lesson_sessions
                WHERE id = {{sessionId}}
                FOR UPDATE SKIP LOCKED
                """)
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(cancellationToken);

        if (session is null
            || session.Status is LessonSessionStatus.Completed or LessonSessionStatus.Cancelled
            || clock.UtcNow < session.EndsAt)
        {
            return await AbandonAsync(transaction, cancellationToken);
        }

        var participants = (await scheduling.ListActiveBookingsAsync(session.Id, cancellationToken))
            .Where(booking => booking.Status is not BookingStatus.Waitlisted)
            .ToList();

        if (participants.Count is 0)
        {
            return await AbandonAsync(transaction, cancellationToken);
        }

        // Yoklamasi kismen girilmis oturum elle tamamlanmak uzere birakilir:
        // otomatik varsayim, insanin bildirdigi gercek katilimin uzerine yazmamali.
        if (participants.Any(booking => booking.Attendance is not null))
        {
            return await AbandonAsync(transaction, cancellationToken);
        }

        var durationMinutes = (int)(session.EndsAt - session.StartsAt).TotalMinutes;

        foreach (var booking in participants)
        {
            scheduling.AddAttendance(SessionAttendance.Create(
                booking.Id,
                AttendanceStatus.Present,
                durationMinutes,
                session.StartsAt,
                session.EndsAt));

            booking.MarkAttended();

            var consumption = await entitlements.ConsumeAsync(booking, cancellationToken);
            if (consumption.IsFailure)
            {
                LogSessionSkipped(logger, sessionId, consumption.Error.Code);
                return await AbandonAsync(transaction, cancellationToken);
            }
        }

        // Ucret tanimi eksikse hakedis uretilemez; oturumu yarim tamamlamak yerine
        // dokunmadan birakip yoneticinin tanimi girmesini bekleriz. Elle tamamlama
        // ucu da ayni kurala tabi, dolayisiyla davranis her iki yolda ayni.
        var earnings = await compensation.CreateEarningsAsync(session.Id, cancellationToken);
        if (earnings.IsFailure)
        {
            LogSessionSkipped(logger, sessionId, earnings.Error.Code);
            return await AbandonAsync(transaction, cancellationToken);
        }

        session.Complete();
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return participants.Count;
    }

    private async Task<int?> AbandonAsync(ITransaction transaction, CancellationToken cancellationToken)
    {
        await transaction.RollbackAsync(cancellationToken);
        context.ChangeTracker.Clear();
        return null;
    }

    /// <remarks>
    /// Sessizce atlamak, ucret tanimi girilmedigi icin hicbir dersin kapanmadigi
    /// durumu gorunmez kilardi; sebep kodu logda tasinir.
    /// </remarks>
    [LoggerMessage(
        EventId = 2203,
        Level = LogLevel.Warning,
        Message = "Session {SessionId} was not auto-completed: {ErrorCode}")]
    private static partial void LogSessionSkipped(ILogger logger, Guid sessionId, string errorCode);
}
