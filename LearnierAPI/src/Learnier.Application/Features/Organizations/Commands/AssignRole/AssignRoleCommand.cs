using FluentValidation;

namespace Learnier.Application.Features.Organizations.Commands.AssignRole;

/// <remarks>
/// Uyelik kimligi komutta degil handler parametresinde tasinir: rotadan geliyor.
/// Komut yalnizca govdeden geleni tutarsa action parametresi olarak baglanabilir
/// ve <c>ValidationFilter</c> kurallari calistirabilir.
/// </remarks>
public sealed record AssignRoleCommand(Guid RoleId);

internal sealed class AssignRoleValidator : AbstractValidator<AssignRoleCommand>
{
    public AssignRoleValidator()
    {
        RuleFor(c => c.RoleId)
            .NotEmpty().WithErrorCode("organization.role_required");
    }
}
