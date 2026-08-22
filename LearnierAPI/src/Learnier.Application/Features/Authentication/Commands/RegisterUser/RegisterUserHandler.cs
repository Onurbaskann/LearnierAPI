using Learnier.Application.Common.Abstractions;
using Learnier.Application.Common.Results;
using Learnier.Domain.Identity;

namespace Learnier.Application.Features.Authentication.Commands.RegisterUser;

/// <summary>
/// Yeni hesap acar ve e-posta dogrulama tokeni gonderir.
/// </summary>
/// <remarks>
/// Hesap <see cref="UserStatus.Pending"/> durumunda olusur ve dogrulanana kadar
/// giris yapamaz; bkz. <c>LoginUserHandler</c>. Dogrulama olmadan sahte
/// e-postalarla hesap acilabilirdi.
/// </remarks>
public sealed class RegisterUserHandler(
    IUserRepository users,
    IEmailVerificationTokenRepository verificationTokens,
    IEmailVerificationTokenFactory verificationTokenFactory,
    IPasswordHasher passwordHasher,
    IEmailSender emailSender,
    IRegistrationMembershipProvisioner membershipProvisioner,
    IUnitOfWork unitOfWork)
{
    public async Task<Result<RegisterUserResult>> Handle(
        RegisterUserCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var existing = await users.FindByEmailAsync(command.Email, cancellationToken);

        if (existing is not null)
        {
            // Cakismayi burada bildirmek hesap sayimina (user enumeration) izin verir.
            // Yine de bilincli tercih: alternatifi, kullanicinin neden giris
            // yapamadigini anlamadigi sessiz bir basari yaniti olurdu. Kayit ucu
            // ileride hiz sinirlamasiyla korunmali.
            return AuthenticationErrors.EmailAlreadyRegistered;
        }

        var user = User.Register(
            command.Email,
            command.FirstName,
            command.LastName,
            passwordHasher.Hash(command.Password));

        users.Add(user);

        var token = verificationTokenFactory.Create();

        verificationTokens.Add(EmailVerificationToken.Issue(
            user.Id,
            token.TokenHash,
            token.IssuedAt,
            token.ExpiresAt));

        await membershipProvisioner.ProvisionAsync(user, cancellationToken);

        // Once kaydet, sonra gonder: gonderim basarisiz olursa kullanici yeniden
        // dogrulama isteyebilir, ama kaydedilmemis bir kullaniciya e-posta gitmesi
        // geri donusu olmayan bir tutarsizlik olurdu.
        await unitOfWork.SaveChangesAsync(cancellationToken);

        await emailSender.SendAsync(
            new EmailNotification(
                user.Email,
                "email.verification",
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["firstName"] = user.FirstName,
                    ["token"] = token.RawToken
                }),
            cancellationToken);

        return new RegisterUserResult(user.Id, user.Email);
    }
}
