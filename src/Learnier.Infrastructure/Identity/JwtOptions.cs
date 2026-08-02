using System.ComponentModel.DataAnnotations;

namespace Learnier.Infrastructure.Identity;

/// <summary>
/// JWT uretim ve dogrulama ayarlari.
/// </summary>
/// <remarks>
/// <see cref="SigningKey"/> bir sirdir: appsettings'e yazilmaz, ortam degiskeni
/// veya sir yoneticisi uzerinden saglanir. Uygulama, gecersiz ayarla baslamasin diye
/// bu tip <c>ValidateOnStart</c> ile dogrulanir.
/// </remarks>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    [Required(AllowEmptyStrings = false)]
    public string Issuer { get; init; } = string.Empty;

    [Required(AllowEmptyStrings = false)]
    public string Audience { get; init; } = string.Empty;

    /// <summary>HMAC-SHA256 icin en az 32 bayt (256 bit) gerekir.</summary>
    [Required(AllowEmptyStrings = false)]
    [MinLength(32)]
    public string SigningKey { get; init; } = string.Empty;

    /// <summary>
    /// Erisim tokeni omru. Kisa tutulur: izinler token'da tasinmasa da
    /// hesap askiya alindiginda etkinin hizla yansimasi icin.
    /// </summary>
    [Range(1, 1440)]
    public int AccessTokenLifetimeMinutes { get; init; } = 15;
}
