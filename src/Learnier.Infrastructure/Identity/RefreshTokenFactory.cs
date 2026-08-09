using System.Security.Cryptography;
using System.Text;
using Learnier.Application.Common.Abstractions;
using Microsoft.Extensions.Options;

namespace Learnier.Infrastructure.Identity;

/// <summary>
/// Kriptografik olarak guvenli yenileme tokeni uretir.
/// </summary>
/// <remarks>
/// <para>
/// Token 32 rastgele bayttan olusur; tahmin edilemez olmasi tek guvenlik dayanagidir,
/// cunku JWT'nin aksine icerigi imzayla dogrulanmaz, yalnizca veritabaninda aranir.
/// </para>
/// <para>
/// Ozetleme icin tuz kullanilmaz ve parola ozetleyicisi tercih edilmez: token zaten
/// yuksek entropili rastgele bir degerdir, sozluk saldirisina acik degildir. Burada
/// gereken sey yavas bir ozet degil, hizli ve sabit bir arama anahtaridir.
/// </para>
/// </remarks>
internal sealed class RefreshTokenFactory(IOptions<JwtOptions> options, IClock clock)
    : IRefreshTokenFactory
{
    private const int TokenSizeInBytes = 32;

    private readonly JwtOptions _options = options.Value;

    public NewRefreshToken Create()
    {
        var raw = Base64UrlEncode(RandomNumberGenerator.GetBytes(TokenSizeInBytes));
        var issuedAt = clock.UtcNow;

        return new NewRefreshToken(
            raw,
            Hash(raw),
            issuedAt,
            issuedAt.AddDays(_options.RefreshTokenLifetimeDays));
    }

    public string Hash(string rawToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rawToken);

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToHexStringLower(hash);
    }

    /// <summary>
    /// URL ve HTTP basliklarinda sorun cikarmayan bicim: <c>+ / =</c> karakterleri yok.
    /// </summary>
    private static string Base64UrlEncode(byte[] bytes)
        => Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
}
