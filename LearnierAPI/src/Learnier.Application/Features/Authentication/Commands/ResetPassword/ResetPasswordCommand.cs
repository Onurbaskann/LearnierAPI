using FluentValidation;

namespace Learnier.Application.Features.Authentication.Commands.ResetPassword;

public sealed record ResetPasswordCommand(string Token, string NewPassword);

internal sealed class ResetPasswordValidator : AbstractValidator<ResetPasswordCommand>
{
    public ResetPasswordValidator()
    {
        RuleFor(c => c.Token)
            .NotEmpty().WithErrorCode("auth.password_reset_token_required");

        RuleFor(c => c.NewPassword)
            .NotEmpty().WithErrorCode("auth.password_required")
            .MinimumLength(8).WithErrorCode("auth.password_too_short")
            .MaximumLength(128).WithErrorCode("auth.password_too_long");
    }
}
