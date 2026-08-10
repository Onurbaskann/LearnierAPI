using Learnier.Domain.Common;

namespace Learnier.Domain.Identity;

/// <summary>
/// E-posta adresinin sahipligini kanitlamak icin kullanilan tek kullanimlik token.
/// </summary>
/// <remarks>
/// <para>
/// Kaydolan kullanici <see cref="UserStatus.Pending"/> durumunda baslar ve giris
/// yapamaz. Bu token tuketilince <see cref="User.ConfirmEmail"/> cagrilir ve hesap
/// kullanilabilir hale gelir.
/// </para>
/// <para>
/// Yenileme tokeninde oldugu gibi ham deger degil ozeti saklanir. Omru kisa tutulur:
/// e-posta kutusuna erisimi olan biri icin bile gecerlilik penceresi dar olmali.
/// </para>
/// </remarks>
public sealed class EmailVerificationToken : Entity
{
    private EmailVerificationToken()
    {
        TokenHash = string.Empty;
    }

    public Guid UserId { get; private set; }

    /// <summary>Token'in SHA-256 ozeti. Ham deger yalnizca kullanicinin e-postasinda bulunur.</summary>
    public string TokenHash { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Kullanildigi an. Dolu ise token bir daha kullanilamaz.</summary>
    public DateTimeOffset? ConsumedAt { get; private set; }

    public User User { get; private set; } = null!;

    public static EmailVerificationToken Issue(
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

        return new EmailVerificationToken
        {
            UserId = userId,
            TokenHash = tokenHash,
            CreatedAt = issuedAt,
            ExpiresAt = expiresAt
        };
    }

    public bool IsUsable(DateTimeOffset now) => ConsumedAt is null && ExpiresAt > now;

    /// <summary>
    /// Token'i tuketilmis isaretler. Tek kullanimlik olmasi, ayni baglantinin
    /// paylasilmasi durumunda tekrar kullanilmasini engeller.
    /// </summary>
    public void Consume(DateTimeOffset consumedAt) => ConsumedAt ??= consumedAt;
}
