using Learnier.Application.Common.Abstractions;
using Learnier.Application.Common.Results;

namespace Learnier.Application.Features.Organizations.Queries;

public sealed record OrganizationRoleListItem(Guid Id, string Code, string Name);

public sealed record OrganizationMemberListItem(
    Guid MembershipId,
    Guid UserId,
    string Email,
    string FirstName,
    string LastName,
    string Status,
    DateTimeOffset? JoinedAt,
    IReadOnlyList<OrganizationRoleListItem> Roles);

public sealed class ListOrganizationRolesHandler(
    IRoleRepository roles,
    ICurrentTenant currentTenant)
{
    public async Task<Result<IReadOnlyList<OrganizationRoleListItem>>> Handle(
        CancellationToken cancellationToken)
    {
        if (currentTenant.OrganizationId is not { } organizationId)
        {
            return Error.Validation("organization.context_required");
        }

        return (await roles.ListUsableAsync(organizationId, cancellationToken))
            .Select(role => new OrganizationRoleListItem(role.Id, role.Code, role.Name))
            .ToList();
    }
}

public sealed class ListOrganizationMembersHandler(IMembershipRepository memberships)
{
    public async Task<Result<IReadOnlyList<OrganizationMemberListItem>>> Handle(
        CancellationToken cancellationToken)
        => (await memberships.ListAsync(cancellationToken))
            .Select(membership => new OrganizationMemberListItem(
                membership.Id,
                membership.UserId,
                membership.User.Email,
                membership.User.FirstName,
                membership.User.LastName,
                membership.Status.ToString(),
                membership.JoinedAt,
                membership.Roles.Select(link => new OrganizationRoleListItem(
                    link.Role.Id,
                    link.Role.Code,
                    link.Role.Name)).ToList()))
            .ToList();
}
