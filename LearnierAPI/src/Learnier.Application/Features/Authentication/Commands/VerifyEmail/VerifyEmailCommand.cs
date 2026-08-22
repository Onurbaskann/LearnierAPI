using FluentValidation;

namespace Learnier.Application.Features.Authentication.Commands.VerifyEmail;

/// <param name="Token">Dogrulama e-postasindaki ham token.</param>
public sealed record VerifyEmailCommand(string Token);

internal sealed class VerifyEmailValidator : AbstractValidator<VerifyEmailCommand>
{
    public VerifyEmailValidator()
    {
        RuleFor(c => c.Token)
            .NotEmpty().WithErrorCode("auth.verification_token_required");
    }
}
