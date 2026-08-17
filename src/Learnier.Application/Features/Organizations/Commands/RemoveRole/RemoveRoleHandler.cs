using Learnier.Application.Common.Abstractions;
using Learnier.Application.Common.Results;

namespace Learnier.Application.Features.Organizations.Commands.RemoveRole;

/// <summary>Uyelikten atanmis bir rolu kaldirir.</summary>
public sealed class RemoveRoleHandler(
    IMembershipRepository memberships,
    IRoleRepository roles,
    IPermissionCacheInvalidator permissionCache,
    ICurrentTenant currentTenant,
    IUnitOfWork unitOfWork)
{
    public async Task<Result> Handle(RemoveRoleCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (currentTenant.OrganizationId is not { } organizationId)
        {
            return Result.Failure(OrganizationErrors.OrganizationContextRequired);
        }

        var membership = await memberships.FindWithRolesAsync(command.MembershipId, cancellationToken);
        if (membership is null)
        {
            return Result.Failure(OrganizationErrors.MembershipNotFound);
        }

        var role = await roles.FindUsableRoleAsync(command.RoleId, organizationId, cancellationToken);
        if (role is null || !membership.Roles.Any(link => link.RoleId == role.Id))
        {
            return Result.Failure(OrganizationErrors.RoleNotUsable);
        }

        membership.RemoveRole(role.Id);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await permissionCache.InvalidateAsync(membership.Id, cancellationToken);

        return Result.Success();
    }
}
