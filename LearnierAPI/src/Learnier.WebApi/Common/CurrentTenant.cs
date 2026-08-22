using Learnier.Application.Common.Abstractions;

namespace Learnier.WebApi.Common;

/// <summary>
/// Istek basina aktif organizasyon. Degeri <c>TenantResolutionMiddleware</c> belirler.
/// </summary>
/// <remarks>
/// Scoped kayitlidir: her istek kendi ornegine sahiptir. AppDbContext bu ornegi
/// okuyarak global query filter'i uygular, bu yuzden middleware'in filtreye ihtiyac
/// duyan herhangi bir sorgudan once calismasi gerekir.
/// </remarks>
internal sealed class CurrentTenant : ICurrentTenant
{
    public Guid? OrganizationId { get; private set; }

    public Guid? MembershipId { get; private set; }

    public bool HasTenant => OrganizationId.HasValue;

    /// <summary>
    /// Aktif organizasyonu belirler. Yalnizca uyelik dogrulandiktan sonra cagrilmali.
    /// </summary>
    public void Set(Guid organizationId, Guid membershipId)
    {
        OrganizationId = organizationId;
        MembershipId = membershipId;
    }
}
