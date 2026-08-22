using System.Globalization;
using Learnier.Application.Common.Abstractions;
using Microsoft.Extensions.Logging;

namespace Learnier.Infrastructure.Notifications;

/// <summary>
/// E-postayi gondermez, loga yazar.
/// </summary>
/// <remarks>
/// <para>
/// Gercek bir saglayici (SMTP veya iletim servisi) baglanana kadar kullanilan
/// yer tutucu. Gelistirme ortaminda ise yarar: dogrulama tokeni loga dustugu icin
/// akis e-posta kutusu olmadan uctan uca denenebilir.
/// </para>
/// <para>
/// <b>Uretimde kullanilmamali.</b> Kayit oldugunu saniyor ama dogrulama e-postasi
/// almayan kullanicilar olusur. Gercek saglayici eklendiginde bu tip yalnizca
/// Development kaydinda birakilmali.
/// </para>
/// </remarks>
internal sealed partial class LoggingEmailSender(ILogger<LoggingEmailSender> logger) : IEmailSender
{
    // Loglar gelistirici icindir: Ingilizce yazilir ve lokalize edilmez.
    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Email not sent (no provider configured). To: {Recipient}, template: {TemplateCode}, parameters: {Parameters}")]
    private static partial void LogEmailNotSent(
        ILogger logger,
        string recipient,
        string templateCode,
        string parameters);

    public Task SendAsync(EmailNotification notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);

        var parameters = string.Join(
            ", ",
            notification.Parameters.Select(p => string.Create(
                CultureInfo.InvariantCulture,
                $"{p.Key}={p.Value}")));

        LogEmailNotSent(logger, notification.Recipient, notification.TemplateCode, parameters);

        return Task.CompletedTask;
    }
}
