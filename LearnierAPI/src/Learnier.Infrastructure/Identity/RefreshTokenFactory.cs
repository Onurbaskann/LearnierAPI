using Learnier.Application.Common.Abstractions;
using Microsoft.Extensions.Options;

namespace Learnier.Infrastructure.Identity;

/// <summary>
/// Yenileme tokeni uretir.
/// </summary>
/// <remarks>
/// Uretim ve ozetleme <see cref="SecureToken"/>'a birakilir; burada yalnizca
/// yenileme tokenine ozgu olan omur bilgisi eklenir.
/// </remarks>
internal sealed class RefreshTokenFactory(IOptions<JwtOptions> options, IClock clock)
    : IRefreshTokenFactory
{
    private readonly JwtOptions _options = options.Value;

    public NewRefreshToken Create()
    {
        var (raw, hash) = SecureToken.Create();
        var issuedAt = clock.UtcNow;

        return new NewRefreshToken(
            raw,
            hash,
            issuedAt,
            issuedAt.AddDays(_options.RefreshTokenLifetimeDays));
    }

    public string Hash(string rawToken) => SecureToken.Hash(rawToken);
}
