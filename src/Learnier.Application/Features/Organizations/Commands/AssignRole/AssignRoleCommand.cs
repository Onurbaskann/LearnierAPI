using FluentValidation;

namespace Learnier.Application.Features.Organizations.Commands.AssignRole;

public sealed record AssignRoleCommand(Guid MembershipId, Guid RoleId);

internal sealed class AssignRoleValidator : AbstractValidator<AssignRoleCommand>
{
    public AssignRoleValidator()
    {
        RuleFor(c => c.MembershipId)
            .NotEmpty().WithErrorCode("organization.membership_required");

        RuleFor(c => c.RoleId)
            .NotEmpty().WithErrorCode("organization.role_required");
    }
}
