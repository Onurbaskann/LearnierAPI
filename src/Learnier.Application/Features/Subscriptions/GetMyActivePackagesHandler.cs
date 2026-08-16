using Learnier.Application.Common.Abstractions;
using Learnier.Application.Common.Results;

namespace Learnier.Application.Features.Subscriptions;

public sealed class GetMyActivePackagesHandler(
    IActivePackageQueries queries,
    ICurrentUser currentUser,
    ICurrentTenant currentTenant)
{
    public async Task<Result<IReadOnlyList<ActivePackageAccess>>> Handle(
        CancellationToken cancellationToken)
    {
        // Bu uc bir izin policy'si tasimadigi icin tenant baglami kendiliginden
        // dogrulanmaz. Global query filter organizasyon yokken devre disi kalir;
        // o yuzden iki kuruma uye bir ogrenci ikisinin paketlerini birden gorurdu.
        if (!currentTenant.HasTenant)
        {
            return Error.Forbidden("tenant.organization_required");
        }

        if (currentUser.UserId is not { } userId)
        {
            return Error.Unauthorized("common.unauthorized");
        }

        return Result<IReadOnlyList<ActivePackageAccess>>.Success(
            await queries.ListForUserAsync(userId, cancellationToken));
    }
}
