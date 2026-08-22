using Learnier.Application.Common.Abstractions;
using Learnier.Application.Common.Results;

namespace Learnier.Application.Features.Authentication.Commands.LogoutUser;

/// <summary>
/// Yenileme tokenini iptal ederek mevcut oturumu sonlandirir.
/// </summary>
public sealed class LogoutUserHandler(
    IRefreshTokenRepository refreshTokens,
    IRefreshTokenFactory refreshTokenFactory,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    public async Task<Result> Handle(LogoutUserCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var hash = refreshTokenFactory.Hash(command.RefreshToken);
        var token = await refreshTokens.FindByHashAsync(hash, cancellationToken);

        // Cikis idempotenttir. Gecersiz, suresi dolmus veya daha once iptal edilmis
        // bir token icin de basarili donulur; tokenin gecmisi disariya sizdirilmaz.
        if (token is null || !token.IsActive(clock.UtcNow))
        {
            return Result.Success();
        }

        token.Revoke(clock.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
