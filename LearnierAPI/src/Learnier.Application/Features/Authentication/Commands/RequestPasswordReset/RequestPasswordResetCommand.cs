using FluentValidation;

namespace Learnier.Application.Features.Authentication.Commands.RequestPasswordReset;

public sealed record RequestPasswordResetCommand(string Email);

internal sealed class RequestPasswordResetValidator : AbstractValidator<RequestPasswordResetCommand>
{
    public RequestPasswordResetValidator()
    {
        RuleFor(c => c.Email)
            .NotEmpty().WithErrorCode("auth.email_required")
            .EmailAddress().WithErrorCode("auth.email_invalid")
            .MaximumLength(320).WithErrorCode("auth.email_too_long");
    }
}
