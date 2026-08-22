using FluentValidation;

namespace Learnier.Application.Features.Organizations.Commands.InviteMember;

/// <param name="Email">Davet edilecek kayitli kullanicinin e-postasi.</param>
public sealed record InviteMemberCommand(string Email, Guid RoleId);

public sealed record InviteMemberResult(Guid MembershipId, Guid UserId);

internal sealed class InviteMemberValidator : AbstractValidator<InviteMemberCommand>
{
    public InviteMemberValidator()
    {
        RuleFor(c => c.Email)
            .NotEmpty().WithErrorCode("auth.email_required")
            .EmailAddress().WithErrorCode("auth.email_invalid");

        RuleFor(c => c.RoleId)
            .NotEmpty().WithErrorCode("organization.role_required");
    }
}
