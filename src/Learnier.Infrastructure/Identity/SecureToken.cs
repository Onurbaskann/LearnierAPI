using System.Security.Cryptography;
using System.Text;

namespace Learnier.Infrastructure.Identity;

/// <summary>
/// Tahmin edilemez token uretimi ve ozetlenmesi.
/// </summary>
/// <remarks>
/// <para>
/// Hem yenileme hem e-posta dogrulama tokenleri ayni ureticiyi kullanir: ikisi de
/// veritabaninda aranan, imzayla dogrulanmayan opak degerlerdir. Tek guvenlik
/// dayanaklari tahmin edilemez olmalari.
/// </para>
/// <para>
/// Ozetlemede tuz kullanilmaz ve parola ozetleyicisi tercih edilmez: token zaten
/// yuksek entropili rastgele bir degerdir, sozluk saldirisina acik degildir. Burada
/// gereken yavas bir ozet degil, hizli ve sabit bir arama anahtaridir.
/// </para>
/// </remarks>
internal static class SecureToken
{
    private const int TokenSizeInBytes = 32;

    public static (string RawToken, string TokenHash) Create()
    {
        var raw = Base64UrlEncode(RandomNumberGenerator.GetBytes(TokenSizeInBytes));
        return (raw, Hash(raw));
    }

    public static string Hash(string rawToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rawToken);

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToHexStringLower(hash);
    }

    /// <summary>
    /// URL ve HTTP basliklarinda sorun cikarmayan bicim: <c>+ / =</c> karakterleri yok.
    /// E-posta dogrulama baglantisinda token adres icinde tasindigi icin bu onemli.
    /// </summary>
    private static string Base64UrlEncode(byte[] bytes)
        => Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
}
