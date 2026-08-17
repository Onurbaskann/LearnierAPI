using Learnier.Application.Common.Abstractions;
using Learnier.Application.Common.Results;

namespace Learnier.Application.Features.Accounts.Queries.GetMyContact;

public sealed class GetMyContactHandler(
    IUserRepository users,
    ICurrentUser currentUser)
{
    public async Task<Result<AccountContact>> Handle(CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Error.Unauthorized("common.unauthorized");
        }

        var user = await users.FindByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            return Error.Unauthorized("common.unauthorized");
        }

        return new AccountContact(
            user.Id, user.Email, user.FirstName, user.LastName, user.Phone);
    }
}
