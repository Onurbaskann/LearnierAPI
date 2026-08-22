using Learnier.Application.Common.Results;

namespace Learnier.Application.Features.Friends;

internal static class FriendshipErrors
{
    public static Error UserNotFound => Error.NotFound("friends.user_not_found");
    public static Error CannotAddSelf => Error.Validation("friends.cannot_add_self");

    /// <summary>Eklenmek istenen hesap ogrenci degil.</summary>
    public static Error NotAStudent => Error.Validation("friends.not_a_student");

    /// <summary>
    /// Istegi gonderen ogrenci degil. Arkadaslik iki tarafi da ogrenci olan bir
    /// iliski; egitmen ve yonetici hesaplari bu akisin disindadir.
    /// </summary>
    public static Error SenderNotAStudent => Error.Forbidden("friends.sender_not_a_student");
    public static Error AlreadyFriends => Error.Conflict("friends.already_friends");
    public static Error RequestAlreadyPending => Error.Conflict("friends.request_already_pending");
    public static Error RequestNotFound => Error.NotFound("friends.request_not_found");
    public static Error RequestNotOwned => Error.Forbidden("friends.request_not_owned");
    public static Error FriendshipNotFound => Error.NotFound("friends.friendship_not_found");
    public static Error FriendshipNotOwned => Error.Forbidden("friends.friendship_not_owned");
}
