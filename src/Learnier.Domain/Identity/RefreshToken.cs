using Learnier.Domain.Common;

namespace Learnier.Domain.Identity;

/// <summary>
/// Erisim tokenini yenilemek icin kullanilan uzun omurlu token.
/// </summary>
/// <remarks>
/// <para>
/// Bu tablo kaynak dokumanda yok; bilincli bir ekleme. Erisim tokeni kisa omurlu
/// tutuldugu icin (15 dakika), yenileme mekanizmasi olmadan kullanicinin her 15
/// dakikada bir parolasini yeniden girmesi gerekirdi.
/// </para>
/// <para>
/// Token'in kendisi degil <b>ozeti</b> saklanir. Veritabani sizsa bile eldeki
/// ozetlerle oturum ele gecirilemez - parola ozetlerinde oldugu gibi.
/// </para>
/// </remarks>
public sealed class RefreshToken : Entity
{
    private RefreshToken()
    {
        TokenHash = string.Empty;
    }

    public Guid UserId { get; private set; }

    /// <summary>Token'in SHA-256 ozeti. Ham token yalnizca istemcide bulunur.</summary>
    public string TokenHash { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    public User User { get; private set; } = null!;

    public static RefreshToken Issue(
        Guid userId,
        string tokenHash,
        DateTimeOffset issuedAt,
        DateTimeOffset expiresAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenHash);

        if (expiresAt <= issuedAt)
        {
            throw new ArgumentException(
                "Token bitis zamani veris zamanindan sonra olmali.",
                nameof(expiresAt));
        }

        return new RefreshToken
        {
            UserId = userId,
            TokenHash = tokenHash,
            CreatedAt = issuedAt,
            ExpiresAt = expiresAt
        };
    }

    public bool IsActive(DateTimeOffset now) => RevokedAt is null && ExpiresAt > now;

    /// <summary>
    /// Token'i iptal eder.
    /// </summary>
    /// <remarks>
    /// Yenileme sirasinda eski token her zaman iptal edilir (rotasyon): calinmis bir
    /// token ikinci kez kullanilamaz ve mesru kullanici yenileme yaptiginda hirsizin
    /// elindeki token gecersiz kalir.
    /// </remarks>
    public void Revoke(DateTimeOffset revokedAt) => RevokedAt ??= revokedAt;
}
