using FluentValidation;

namespace Learnier.Application.Features.Organizations.Commands.RemoveRole;

public sealed record RemoveRoleCommand(Guid MembershipId, Guid RoleId);

internal sealed class RemoveRoleValidator : AbstractValidator<RemoveRoleCommand>
{
    public RemoveRoleValidator()
    {
        RuleFor(c => c.MembershipId)
            .NotEmpty().WithErrorCode("organization.membership_required");

        RuleFor(c => c.RoleId)
            .NotEmpty().WithErrorCode("organization.role_required");
    }
}
