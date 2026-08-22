using FluentValidation;

namespace Learnier.Application.Features.Authentication.Commands.LogoutUser;

public sealed record LogoutUserCommand(string RefreshToken);

internal sealed class LogoutUserValidator : AbstractValidator<LogoutUserCommand>
{
    public LogoutUserValidator()
    {
        RuleFor(c => c.RefreshToken)
            .NotEmpty().WithErrorCode("auth.refresh_token_required");
    }
}
