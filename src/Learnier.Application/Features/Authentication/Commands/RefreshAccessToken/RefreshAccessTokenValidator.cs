using FluentValidation;

namespace Learnier.Application.Features.Authentication.Commands.RefreshAccessToken;

internal sealed class RefreshAccessTokenValidator : AbstractValidator<RefreshAccessTokenCommand>
{
    public RefreshAccessTokenValidator()
    {
        RuleFor(c => c.RefreshToken)
            .NotEmpty().WithErrorCode("auth.refresh_token_required");
    }
}
