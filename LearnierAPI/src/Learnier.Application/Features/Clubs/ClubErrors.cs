using Learnier.Application.Common.Results;

namespace Learnier.Application.Features.Clubs;

/// <summary>
/// Kulup islemlerinin hata kodlari.
/// </summary>
internal static class ClubErrors
{
    public static Error OrganizationContextRequired
        => Error.Forbidden("tenant.organization_required");

    public static Error SubjectNotFound
        => Error.Validation("clubs.subject_not_found");

    public static Error AlreadyExistsForSubject
        => Error.Conflict("clubs.already_exists_for_subject");

    public static Error ClubNotFound
        => Error.NotFound("clubs.club_not_found");

    public static Error AlreadyClosed
        => Error.Conflict("clubs.already_closed");

    public static Error AlreadyOpen
        => Error.Conflict("clubs.already_open");

    public static Error PackageRequired
        => Error.Forbidden("clubs.package_required");

    public static Error RoomNotFound
        => Error.NotFound("clubs.room_not_found");

    public static Error RoomAlreadyExists
        => Error.Conflict("clubs.room_already_exists");

    public static Error TextRoomRequired
        => Error.Validation("clubs.text_room_required");
}
