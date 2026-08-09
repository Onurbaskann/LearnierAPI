using Learnier.Application.Common.Abstractions;
using Learnier.Application.Common.Results;
using Learnier.Domain.Identity;

namespace Learnier.Application.Features.Authentication.Commands.RefreshAccessToken;

/// <summary>
/// Yenileme tokeni ile yeni bir erisim tokeni verir.
/// </summary>
/// <remarks>
/// Kullanilan yenileme tokeni her zaman iptal edilir ve yerine yenisi verilir
/// (rotasyon). Boylece calinmis bir token ikinci kez ise yaramaz.
/// </remarks>
public sealed class RefreshAccessTokenHandler(
    IRefreshTokenRepository refreshTokens,
    IRefreshTokenFactory refreshTokenFactory,
    IUserRepository users,
    ITokenService tokenService,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    public async Task<Result<RefreshAccessTokenResult>> Handle(
        RefreshAccessTokenCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var now = clock.UtcNow;
        var hash = refreshTokenFactory.Hash(command.RefreshToken);

        var existing = await refreshTokens.FindByHashAsync(hash, cancellationToken);

        if (existing is null || !existing.IsActive(now))
        {
            // Bulunamadi, suresi doldu ve iptal edilmis durumlari ayirt edilmez:
            // ayrintili yanit, elinde gecersiz token olan birine hangi tokenin
            // bir zamanlar gecerli oldugunu soylerdi.
            return AuthenticationErrors.InvalidRefreshToken;
        }

        var user = await users.FindByIdAsync(existing.UserId, cancellationToken);

        if (user is null || user.Status is not UserStatus.Active)
        {
            // Hesap askiya alinmis veya dogrulamasi geri alinmissa yenileme
            // yapilmamali; aksi halde askiya alma islemi bir sonraki erisim
            // tokeninin suresi dolana kadar etkisiz kalirdi.
            return AuthenticationErrors.InvalidRefreshToken;
        }

        existing.Revoke(now);

        var accessToken = tokenService.CreateAccessToken(user.Id, user.Email);
        var replacement = refreshTokenFactory.Create();

        refreshTokens.Add(RefreshToken.Issue(
            user.Id,
            replacement.TokenHash,
            replacement.IssuedAt,
            replacement.ExpiresAt));

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new RefreshAccessTokenResult(
            accessToken.Value,
            accessToken.ExpiresAt,
            replacement.RawToken);
    }
}
