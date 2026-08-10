using Learnier.Application.Common.Abstractions;
using Learnier.Application.Common.Results;
using Learnier.Domain.Identity;

namespace Learnier.Application.Features.Authentication.Commands.RegisterUser;

/// <summary>
/// Yeni ve hemen kullanilabilir bir hesap acar.
/// </summary>
/// <remarks>
/// Acik kayit akisinda e-posta dogrulamasi aranmaz; hesap kayitla birlikte aktif olur.
/// </remarks>
public sealed class RegisterUserHandler(
    IUserRepository users,
    IPasswordHasher passwordHasher,
    IRegistrationMembershipProvisioner membershipProvisioner,
    IClock clock,
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

        user.ConfirmEmail(clock.UtcNow);

        users.Add(user);

        await membershipProvisioner.ProvisionAsync(user, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new RegisterUserResult(user.Id, user.Email);
    }
}
