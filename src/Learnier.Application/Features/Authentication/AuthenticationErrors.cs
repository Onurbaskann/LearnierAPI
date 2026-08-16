using Learnier.Application.Common.Results;

namespace Learnier.Application.Features.Authentication;

/// <summary>
/// Kimlik dogrulama akisinin hata kodlari.
/// </summary>
/// <remarks>
/// Kodlarin karsiligi <c>Localization.ErrorMessages</c> kaynak dosyalarindadir;
/// bu katman metin uretmez.
/// </remarks>
internal static class AuthenticationErrors
{
    /// <summary>
    /// Kullanici bulunamadi <b>veya</b> parola yanlis.
    /// </summary>
    /// <remarks>
    /// Iki durum icin ayni kod bilerek donuluyor. Ayirt edilseydi istemci
    /// "bu e-posta kayitli mi" sorusunu yanitlayabilir, yani hesap sayimi
    /// (user enumeration) mumkun olurdu.
    /// </remarks>
    public static Error InvalidCredentials => Error.Unauthorized("auth.invalid_credentials");

    public static Error AccountSuspended => Error.Forbidden("auth.account_suspended");

    public static Error AccountInactive => Error.Forbidden("auth.account_inactive");

    /// <summary>
    /// Yenileme tokeni bulunamadi, suresi doldu veya iptal edilmis.
    /// </summary>
    /// <remarks>
    /// Uc durum icin ayni kod bilerek donuluyor: ayirt edilseydi, elinde gecersiz
    /// bir token olan biri o tokenin bir zamanlar gecerli olup olmadigini ogrenirdi.
    /// </remarks>
    public static Error InvalidRefreshToken => Error.Unauthorized("auth.invalid_refresh_token");

    public static Error EmailAlreadyRegistered => Error.Conflict("auth.email_already_registered");

    public static Error InvalidPasswordResetToken
        => Error.Validation("auth.invalid_password_reset_token");

    /// <summary>
    /// Hesap acilmis ancak e-posta dogrulanmamis.
    /// </summary>
    public static Error EmailNotVerified => Error.Forbidden("auth.email_not_verified");

    /// <summary>
    /// Dogrulama tokeni bulunamadi, suresi doldu veya daha once kullanilmis.
    /// </summary>
    /// <remarks>
    /// Uc durum icin ayni kod donuluyor; gerekcesi <see cref="InvalidRefreshToken"/>
    /// ile ayni.
    /// </remarks>
    public static Error InvalidVerificationToken
        => Error.Validation("auth.invalid_verification_token");
}
