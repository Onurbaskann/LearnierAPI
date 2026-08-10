namespace Learnier.Application.Common.Abstractions;

/// <summary>
/// Kullaniciya e-posta gonderir.
/// </summary>
/// <remarks>
/// Gonderim eszamanli yapilir; bu, kullanici sayisi arttiginda kuyruga tasinmasi
/// gereken bir noktadir. Su an icin kayit akisinin disina cikmamasi tercih edildi:
/// kuyruk altyapisi olmadan "gonderildi sanilan ama gonderilmemis" e-postalar
/// olusabilirdi.
/// </remarks>
public interface IEmailSender
{
    Task SendAsync(EmailNotification notification, CancellationToken cancellationToken);
}

/// <summary>
/// Gonderilecek e-postanin icerigi degil <b>tanimi</b>.
/// </summary>
/// <remarks>
/// Metin degil sablon kodu ve parametreler tasinir. Sebep, hata kodlarindakiyle ayni:
/// Application katmani kullaniciya gorunen metin uretmez, boylece yeni bir dil
/// eklendiginde is mantigina dokunulmasi gerekmez. Sablonun metne cevrilmesi
/// gonderim implementasyonuna aittir.
/// </remarks>
/// <param name="TemplateCode">Ornegin <c>email.verification</c>.</param>
public sealed record EmailNotification(
    string Recipient,
    string TemplateCode,
    IReadOnlyDictionary<string, string> Parameters);
