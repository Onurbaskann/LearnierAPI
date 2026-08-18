namespace Learnier.Application.Common.Abstractions;

/// <param name="ScannedSessions">Aday olarak taranan oturum sayisi.</param>
/// <param name="CompletedSessions">Tamamlanan oturum sayisi.</param>
/// <param name="CompletedBookings">Yoklamasi olusturulan rezervasyon sayisi.</param>
/// <param name="SkippedSessions">
/// Elle mudahale bekledigi icin atlanan oturum sayisi — ornegin yoklamasi
/// kismen girilmis ya da ucret tanimi eksik oturumlar.
/// </param>
public sealed record SessionCompletionResult(
    int ScannedSessions,
    int CompletedSessions,
    int CompletedBookings,
    int SkippedSessions);

/// <summary>
/// Suresi dolmus ders oturumlarini otomatik tamamlar.
/// </summary>
/// <remarks>
/// Tamamlama elle bir adim olarak tasarlanmisti; kimse cagirmadiginda oturum
/// sonsuza dek <c>Scheduled</c>/<c>Confirmed</c> kaliyor, bu yuzden yoklama
/// yazilmiyor, ayrilmis kredi tuketilmiyor ve egitmen hakedisi olusmuyordu.
/// Bu islem o zinciri egitmen onayina birakmadan kapatir.
/// </remarks>
public interface ISessionCompletionProcessor
{
    Task<SessionCompletionResult> ProcessDueAsync(
        int batchSize,
        TimeSpan gracePeriod,
        CancellationToken cancellationToken);
}
