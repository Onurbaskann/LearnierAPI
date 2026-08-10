using Learnier.Domain.Identity;

namespace Learnier.Application.Common.Abstractions;

public interface IOrganizationRepository
{
    Task<bool> SlugExistsAsync(string slug, CancellationToken cancellationToken);

    /// <summary>
    /// Organizasyonu uyelikleriyle birlikte getirir.
    /// </summary>
    /// <remarks>
    /// Uye ekleme aggregate uzerinden yapildigi icin koleksiyonun yuklu olmasi gerekir.
    /// </remarks>
    Task<Organization?> FindWithMembershipsAsync(Guid organizationId, CancellationToken cancellationToken);

    void Add(Organization organization);
}

public interface IRoleRepository
{
    /// <summary>
    /// Koda gore sistem rolunu bulur. Sistem rollerinin organizasyonu yoktur ve
    /// her kurumda kullanilabilirler.
    /// </summary>
    Task<Role?> FindSystemRoleByCodeAsync(string code, CancellationToken cancellationToken);

    /// <summary>
    /// Verilen organizasyonda kullanilabilir bir rolu bulur.
    /// </summary>
    /// <remarks>
    /// Rol ya sistem rolu olmali ya da o organizasyona ait olmali; baska bir kurumun
    /// ozel rolu buraya atanamaz.
    /// </remarks>
    Task<Role?> FindUsableRoleAsync(Guid roleId, Guid organizationId, CancellationToken cancellationToken);
}

public interface IMembershipRepository
{
    /// <summary>
    /// Uyeligi rolleriyle birlikte getirir.
    /// </summary>
    /// <remarks>
    /// Sorgu kiraci filtresine tabidir: baska bir organizasyonun uyeligi bulunamaz.
    /// </remarks>
    Task<OrganizationMembership?> FindWithRolesAsync(Guid membershipId, CancellationToken cancellationToken);

    Task<bool> ExistsAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken);
}
