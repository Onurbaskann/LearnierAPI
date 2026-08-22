using Learnier.Application.Common.Abstractions;
using Learnier.Application.Common.Results;
using Learnier.Domain.Identity;

namespace Learnier.Application.Features.Authentication.Commands.RequestPasswordReset;

public sealed class RequestPasswordResetHandler(
    IUserRepository users,
    IPasswordResetTokenStore tokens,
    IEmailSender emailSender)
{
    public async Task<Result> Handle(
        RequestPasswordResetCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var user = await users.FindByEmailAsync(command.Email, cancellationToken);

        // Hesap bulunamasa veya kullanilamaz durumda olsa da ayni yanit doner.
        // Boylece bu uc kayitli e-posta adreslerini ifsa etmez.
        if (user is null || user.Status is not UserStatus.Active)
        {
            return Result.Success();
        }

        var token = tokens.Issue(user.Id);

        await emailSender.SendAsync(
            new EmailNotification(
                user.Email,
                "email.password_reset",
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["firstName"] = user.FirstName,
                    ["token"] = token.RawToken,
                    ["expiresAt"] = token.ExpiresAt.ToString("O", System.Globalization.CultureInfo.InvariantCulture)
                }),
            cancellationToken);

        return Result.Success();
    }
}
