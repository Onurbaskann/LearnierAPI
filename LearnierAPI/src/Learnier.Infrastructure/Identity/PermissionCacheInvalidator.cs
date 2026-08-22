using Learnier.Application.Common.Abstractions;
using Microsoft.Extensions.Caching.Hybrid;

namespace Learnier.Infrastructure.Identity;

/// <summary>
/// Izin onbellegini uyelik etiketine gore duser.
/// </summary>
/// <remarks>
/// Etiket <see cref="EfPermissionProvider.MembershipTagPrefix"/> ile uretilir;
/// iki tarafin ayni anahtari kullandigi tek yer orasi.
/// </remarks>
internal sealed class PermissionCacheInvalidator(HybridCache cache) : IPermissionCacheInvalidator
{
    public ValueTask InvalidateAsync(Guid membershipId, CancellationToken cancellationToken)
        => cache.RemoveByTagAsync(
            $"{EfPermissionProvider.MembershipTagPrefix}{membershipId}",
            cancellationToken);
}
