using Learnier.Application.Common.Abstractions;
using Learnier.Application.Common.Results;

namespace Learnier.Application.Features.Authentication.Commands.ResetPassword;

public sealed class ResetPasswordHandler(
    IPasswordResetTokenStore tokens,
    IUserRepository users,
    IRefreshTokenRepository refreshTokens,
    IPasswordHasher passwordHasher,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    public async Task<Result> Handle(ResetPasswordCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var userId = tokens.Consume(command.Token);
        if (userId is null)
        {
            return AuthenticationErrors.InvalidPasswordResetToken;
        }

        var user = await users.FindByIdAsync(userId.Value, cancellationToken);
        if (user is null)
        {
            return AuthenticationErrors.InvalidPasswordResetToken;
        }

        user.ChangePasswordHash(passwordHasher.Hash(command.NewPassword));

        var now = clock.UtcNow;
        var activeRefreshTokens = await refreshTokens.FindActiveByUserIdAsync(
            user.Id,
            now,
            cancellationToken);

        foreach (var refreshToken in activeRefreshTokens)
        {
            refreshToken.Revoke(now);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
