using Learnier.Application.Common.Abstractions;
using Learnier.Application.Common.Results;

namespace Learnier.Application.Features.Subscriptions;

public sealed class GetMyActivePackagesHandler(
    IActivePackageQueries queries,
    ICurrentUser currentUser)
{
    public async Task<Result<IReadOnlyList<ActivePackageAccess>>> Handle(
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Error.Unauthorized("common.unauthorized");
        }

        return Result<IReadOnlyList<ActivePackageAccess>>.Success(
            await queries.ListForUserAsync(userId, cancellationToken));
    }
}
