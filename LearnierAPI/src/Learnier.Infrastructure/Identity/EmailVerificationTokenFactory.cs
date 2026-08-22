using Learnier.Application.Common.Abstractions;
using Microsoft.Extensions.Options;

namespace Learnier.Infrastructure.Identity;

/// <summary>
/// E-posta dogrulama tokeni uretir.
/// </summary>
/// <remarks>
/// Omur bilerek kisa: token e-posta kutusunda duran, tek basina hesabi aktive eden
/// bir sirdir. Suresi dolarsa kullanici yeni bir dogrulama isteyebilir.
/// </remarks>
internal sealed class EmailVerificationTokenFactory(IOptions<JwtOptions> options, IClock clock)
    : IEmailVerificationTokenFactory
{
    private readonly JwtOptions _options = options.Value;

    public NewEmailVerificationToken Create()
    {
        var (raw, hash) = SecureToken.Create();
        var issuedAt = clock.UtcNow;

        return new NewEmailVerificationToken(
            raw,
            hash,
            issuedAt,
            issuedAt.AddHours(_options.EmailVerificationTokenLifetimeHours));
    }

    public string Hash(string rawToken) => SecureToken.Hash(rawToken);
}
