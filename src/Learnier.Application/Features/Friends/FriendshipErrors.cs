using Learnier.Application.Common.Results;

namespace Learnier.Application.Features.Friends;

internal static class FriendshipErrors
{
    public static Error UserNotFound => Error.NotFound("friends.user_not_found");
    public static Error CannotAddSelf => Error.Validation("friends.cannot_add_self");
    public static Error AlreadyFriends => Error.Conflict("friends.already_friends");
    public static Error RequestAlreadyPending => Error.Conflict("friends.request_already_pending");
    public static Error RequestNotFound => Error.NotFound("friends.request_not_found");
    public static Error RequestNotOwned => Error.Forbidden("friends.request_not_owned");
}
