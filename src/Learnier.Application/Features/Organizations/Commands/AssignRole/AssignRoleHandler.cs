using Learnier.Application.Common.Abstractions;
using Learnier.Application.Common.Results;

namespace Learnier.Application.Features.Organizations.Commands.AssignRole;

/// <summary>
/// Mevcut bir uyelige rol ekler.
/// </summary>
public sealed class AssignRoleHandler(
    IMembershipRepository memberships,
    IRoleRepository roles,
    IPermissionCacheInvalidator permissionCache,
    ICurrentTenant currentTenant,
    IUnitOfWork unitOfWork)
{
    public async Task<Result> Handle(AssignRoleCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (currentTenant.OrganizationId is not { } organizationId)
        {
            return Result.Failure(OrganizationErrors.OrganizationContextRequired);
        }

        // Sorgu kiraci filtresine tabi: baska bir organizasyonun uyeligi buradan
        // bulunamaz, yani kimlik bilinse bile yabanci bir uyelige rol atanamaz.
        var membership = await memberships.FindWithRolesAsync(command.MembershipId, cancellationToken);

        if (membership is null)
        {
            return Result.Failure(OrganizationErrors.MembershipNotFound);
        }

        var role = await roles.FindUsableRoleAsync(command.RoleId, organizationId, cancellationToken);

        if (role is null)
        {
            return Result.Failure(OrganizationErrors.RoleNotUsable);
        }

        membership.AssignRole(role.Id);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        // Kayittan sonra dusurulur: once dusurulseydi, kayit ile dusurme arasinda
        // gelen bir istek eski izinleri yeniden onbellege alabilirdi.
        await permissionCache.InvalidateAsync(membership.Id, cancellationToken);

        return Result.Success();
    }
}
