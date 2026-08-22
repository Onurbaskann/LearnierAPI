using Learnier.Application.Common.Abstractions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Learnier.Infrastructure.Identity;

/// <summary>
/// Parola sifirlama tokenlarini yalnizca uygulama belleginde saklar.
/// </summary>
/// <remarks>
/// Ham token cache'e yazilmaz; anahtar olarak SHA-256 ozeti kullanilir. Uygulama
/// yeniden baslarsa bekleyen tokenlar bilincli olarak gecersizlesir. Birden fazla
/// API ornegi kullanildiginda bu implementasyon dagitik cache ile degistirilmelidir.
/// </remarks>
internal sealed class MemoryPasswordResetTokenStore(
    IMemoryCache cache,
    IClock clock,
    IOptions<PasswordResetOptions> options)
    : IPasswordResetTokenStore
{
    private const string TokenKeyPrefix = "auth:password-reset:token:";
    private const string UserKeyPrefix = "auth:password-reset:user:";

    private readonly object _gate = new();
    private readonly PasswordResetOptions _options = options.Value;

    public NewPasswordResetToken Issue(Guid userId)
    {
        var (rawToken, tokenHash) = SecureToken.Create();
        var expiresAt = clock.UtcNow.AddMinutes(_options.TokenLifetimeMinutes);
        var tokenKey = TokenKeyPrefix + tokenHash;
        var userKey = UserKeyPrefix + userId.ToString("N");

        lock (_gate)
        {
            // Her kullanicinin yalnizca son talebi gecerli kalir.
            if (cache.TryGetValue<string>(userKey, out var previousHash)
                && previousHash is not null)
            {
                cache.Remove(TokenKeyPrefix + previousHash);
            }

            var entryOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpiration = expiresAt
            };

            cache.Set(tokenKey, userId, entryOptions);
            cache.Set(userKey, tokenHash, entryOptions);
        }

        return new NewPasswordResetToken(rawToken, expiresAt);
    }

    public Guid? Consume(string rawToken)
    {
        if (string.IsNullOrWhiteSpace(rawToken))
        {
            return null;
        }

        var tokenHash = SecureToken.Hash(rawToken);
        var tokenKey = TokenKeyPrefix + tokenHash;

        lock (_gate)
        {
            if (!cache.TryGetValue<Guid>(tokenKey, out var userId) || userId == Guid.Empty)
            {
                return null;
            }

            cache.Remove(tokenKey);

            var userKey = UserKeyPrefix + userId.ToString("N");
            if (cache.TryGetValue<string>(userKey, out var currentHash)
                && string.Equals(currentHash, tokenHash, StringComparison.Ordinal))
            {
                cache.Remove(userKey);
            }

            return userId;
        }
    }
}
