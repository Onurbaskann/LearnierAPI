using Learnier.Application.Common.Abstractions;
using Learnier.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace Learnier.Infrastructure.Persistence.Repositories;

/// <inheritdoc cref="IOrganizationRepository"/>
internal sealed class EfOrganizationRepository(AppDbContext context) : IOrganizationRepository
{
    public async Task<bool> SlugExistsAsync(string slug, CancellationToken cancellationToken)
        => await context.Organizations.AnyAsync(o => o.Slug == slug, cancellationToken);

    public async Task<Organization?> FindWithMembershipsAsync(
        Guid organizationId,
        CancellationToken cancellationToken)
        => await context.Organizations
            .Include(o => o.Memberships)
            .FirstOrDefaultAsync(o => o.Id == organizationId, cancellationToken);

    public void Add(Organization organization) => context.Organizations.Add(organization);
}

/// <inheritdoc cref="IRoleRepository"/>
internal sealed class EfRoleRepository(AppDbContext context) : IRoleRepository
{
    public async Task<Role?> FindSystemRoleByCodeAsync(string code, CancellationToken cancellationToken)
        => await context.Roles
            .FirstOrDefaultAsync(r => r.Code == code && r.OrganizationId == null, cancellationToken);

    // Sistem rolleri her organizasyonda, ozel roller yalnizca sahibi olan kurumda
    // kullanilabilir. Ayni kural Role.IsVisibleTo icinde de ifade edilir; burada
    // sorguya cevrilebilmesi icin acikca yaziliyor.
    public async Task<Role?> FindUsableRoleAsync(
        Guid roleId,
        Guid organizationId,
        CancellationToken cancellationToken)
        => await context.Roles
            .FirstOrDefaultAsync(
                r => r.Id == roleId
                     && (r.OrganizationId == null || r.OrganizationId == organizationId),
                cancellationToken);
}

/// <inheritdoc cref="IMembershipRepository"/>
internal sealed class EfMembershipRepository(AppDbContext context) : IMembershipRepository
{
    public async Task<OrganizationMembership?> FindWithRolesAsync(
        Guid membershipId,
        CancellationToken cancellationToken)
        => await context.Memberships
            .Include(m => m.Roles)
            .FirstOrDefaultAsync(m => m.Id == membershipId, cancellationToken);

    public async Task<bool> ExistsAsync(
        Guid organizationId,
        Guid userId,
        CancellationToken cancellationToken)
        => await context.Memberships
            .AnyAsync(
                m => m.OrganizationId == organizationId && m.UserId == userId,
                cancellationToken);
}
