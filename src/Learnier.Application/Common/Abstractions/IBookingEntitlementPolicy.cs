using Learnier.Application.Common.Results;
using Learnier.Domain.Scheduling;

namespace Learnier.Application.Common.Abstractions;

/// <summary>
/// Ogrencinin bir oturuma rezervasyon hakki olup olmadigini belirler.
/// </summary>
/// <remarks>
/// <para>
/// Kaynak dokumanin 1. bolumundeki temel karar burada korunuyor: <b>abonelik ile
/// rezervasyon birbirine yapistirilmiyor.</b> Rezervasyon motoru "neyle odendigini"
/// kaydeder ama abonelik kurallarini bilmez; o kurallar bu soyutlamanin arkasindadir.
/// </para>
/// <para>
/// Faz 3'te izin veren basit bir implementasyon kullaniliyor. Abonelik, plan kapsami
/// ve kredi defteri Faz 4'te yazildiginda yalnizca bu tipin implementasyonu degisir;
/// rezervasyon akisina dokunulmaz.
/// </para>
/// </remarks>
public interface IBookingEntitlementPolicy
{
    /// <summary>
    /// Rezervasyona izin veriyorsa hangi hakla odenecegini dondurur.
    /// </summary>
    Task<Result<BookingGrant>> AuthorizeAsync(
        Guid learnerUserId,
        LessonSession session,
        CancellationToken cancellationToken);

    /// <summary>
    /// Iptal edilen rezervasyonun hakkini iade eder.
    /// </summary>
    /// <param name="refundable">
    /// Iptal, ucretsiz iptal sinirindan once yapildiysa dogru. Yanlissa hak yanar;
    /// karar rezervasyon akisinda verilir, iadenin nasil islenecegi burada.
    /// </param>
    Task ReleaseAsync(SessionBooking booking, bool refundable, CancellationToken cancellationToken);
}

/// <param name="AccessSource">Rezervasyonun dayandigi hak turu.</param>
/// <param name="SubscriptionId">Abonelik kapsamindaki rezervasyonlarda dolu.</param>
public sealed record BookingGrant(
    BookingAccessSource AccessSource,
    Guid? SubscriptionId = null);
