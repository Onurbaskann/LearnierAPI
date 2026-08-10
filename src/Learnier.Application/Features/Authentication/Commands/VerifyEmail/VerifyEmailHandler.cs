using Learnier.Application.Common.Abstractions;
using Learnier.Application.Common.Results;

namespace Learnier.Application.Features.Authentication.Commands.VerifyEmail;

/// <summary>
/// E-posta dogrulama tokenini tuketir ve hesabi kullanilabilir hale getirir.
/// </summary>
/// <remarks>
/// Token tek kullanimliktir: ayni baglanti ikinci kez acildiginda gecersiz sayilir.
/// </remarks>
public sealed class VerifyEmailHandler(
    IEmailVerificationTokenRepository verificationTokens,
    IEmailVerificationTokenFactory verificationTokenFactory,
    IUserRepository users,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    public async Task<Result> Handle(VerifyEmailCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var now = clock.UtcNow;

        // Veritabaninda ozet saklandigi icin arama ham token ile degil ozetiyle yapilir.
        var hash = verificationTokenFactory.Hash(command.Token);
        var token = await verificationTokens.FindByHashAsync(hash, cancellationToken);

        if (token is null || !token.IsUsable(now))
        {
            // Bulunamadi, suresi doldu ve kullanilmis durumlari ayni kodu doner:
            // ayirt edilseydi, elinde gecersiz token olan biri o tokenin bir
            // zamanlar gecerli oldugunu ogrenirdi.
            return Result.Failure(AuthenticationErrors.InvalidVerificationToken);
        }

        var user = await users.FindByIdAsync(token.UserId, cancellationToken);

        if (user is null)
        {
            return Result.Failure(AuthenticationErrors.InvalidVerificationToken);
        }

        token.Consume(now);

        // ConfirmEmail askiya alinmis hesabi aktiflestirmez; yalnizca Pending
        // durumundakini gecirir.
        user.ConfirmEmail(now);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
