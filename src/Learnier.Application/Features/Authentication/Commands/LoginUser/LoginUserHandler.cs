using Learnier.Application.Common.Abstractions;
using Learnier.Application.Common.Results;
using Learnier.Domain.Identity;

namespace Learnier.Application.Features.Authentication.Commands.LoginUser;

/// <summary>
/// E-posta ve parola ile giris yapar, erisim tokeni uretir.
/// </summary>
/// <remarks>
/// Handler <c>public</c>: controller onu action parametresinde <c>[FromServices]</c>
/// ile aldigi icin WebApi katmanindan gorunur olmali.
/// </remarks>
public sealed class LoginUserHandler(
    IUserRepository users,
    IRefreshTokenRepository refreshTokens,
    IRefreshTokenFactory refreshTokenFactory,
    IPasswordHasher passwordHasher,
    ITokenService tokenService,
    IUnitOfWork unitOfWork)
{
    public async Task<Result<LoginUserResult>> Handle(
        LoginUserCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var user = await users.FindByEmailAsync(command.Email, cancellationToken);

        // Harici kimlik saglayicisiyla acilmis hesaplarda parola ozeti bulunmaz;
        // boyle bir hesaba parolayla giris yapilamaz.
        if (user?.PasswordHash is null)
        {
            // Kullanici bulunamadiginda da ozetleme maliyeti odenir. Aksi halde yanit
            // suresi "bu e-posta kayitli mi" sorusunu ele verir ve hata kodunu ayni
            // tutmanin anlami kalmazdi.
            _ = passwordHasher.Hash(command.Password);
            return AuthenticationErrors.InvalidCredentials;
        }

        var verification = passwordHasher.Verify(user.PasswordHash, command.Password);

        if (verification is PasswordVerificationOutcome.Failed)
        {
            return AuthenticationErrors.InvalidCredentials;
        }

        // Durum kontrolu parola dogrulandiktan sonra: aksi halde yanlis parola giren
        // biri, hesabin askida oldugunu ogrenirdi.
        if (user.Status is UserStatus.Suspended)
        {
            return AuthenticationErrors.AccountSuspended;
        }

        if (user.Status is UserStatus.Pending)
        {
            return AuthenticationErrors.AccountInactive;
        }

        if (verification is PasswordVerificationOutcome.SuccessRehashNeeded)
        {
            // Parola dogru ancak ozet eski parametrelerle uretilmis. Kullanici
            // farkinda olmadan guncel algoritmaya tasinir.
            user.ChangePasswordHash(passwordHasher.Hash(command.Password));
        }

        var memberships = await users.GetActiveMembershipsAsync(user.Id, cancellationToken);
        var token = tokenService.CreateAccessToken(user.Id, user.Email);
        var refreshToken = refreshTokenFactory.Create();

        refreshTokens.Add(RefreshToken.Issue(
            user.Id,
            refreshToken.TokenHash,
            refreshToken.IssuedAt,
            refreshToken.ExpiresAt));

        // Tek kayit: olasi parola ozeti yenilemesi ve yeni yenileme tokeni
        // ayni islemde yazilir.
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new LoginUserResult(
            token.Value,
            token.ExpiresAt,
            refreshToken.RawToken,
            new AuthenticatedUser(user.Id, user.Email, user.FirstName, user.LastName),
            memberships);
    }
}
