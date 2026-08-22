using FluentValidation;
using Learnier.Application.Common.Abstractions;
using Learnier.Application.Common.Results;
using Learnier.Domain.Social;

namespace Learnier.Application.Features.Clubs;

public sealed record ClubRoomItem(Guid Id, string Name, ClubRoomType Type, int SortOrder);

public sealed record ClubListItem(
    Guid Id,
    Guid SubjectId,
    string SubjectName,
    string Name,
    string Description,
    bool IsActive,
    int MemberCount,
    IReadOnlyList<ClubRoomItem> Rooms);

public sealed record ClubMessageItem(
    Guid Id,
    Guid RoomId,
    Guid AuthorUserId,
    string AuthorName,
    string Body,
    DateTimeOffset CreatedAt);

public sealed record UpdateClubCommand(string Name, string? Description);
public sealed record AddClubRoomCommand(string Name, ClubRoomType Type, int SortOrder);
public sealed record SendClubMessageCommand(string Body);

internal sealed class UpdateClubValidator : AbstractValidator<UpdateClubCommand>
{
    public UpdateClubValidator()
    {
        RuleFor(command => command.Name)
            .NotEmpty().WithErrorCode("clubs.name_required")
            .MaximumLength(200).WithErrorCode("clubs.name_too_long");
        RuleFor(command => command.Description)
            .MaximumLength(1000).WithErrorCode("clubs.description_too_long");
    }
}

internal sealed class AddClubRoomValidator : AbstractValidator<AddClubRoomCommand>
{
    public AddClubRoomValidator()
    {
        RuleFor(command => command.Name)
            .NotEmpty().WithErrorCode("clubs.room_name_required")
            .MaximumLength(100).WithErrorCode("clubs.room_name_too_long");
        RuleFor(command => command.Type)
            .IsInEnum().WithErrorCode("clubs.room_type_invalid");
        RuleFor(command => command.SortOrder)
            .GreaterThanOrEqualTo(0).WithErrorCode("clubs.room_sort_order_invalid");
    }
}

internal sealed class SendClubMessageValidator : AbstractValidator<SendClubMessageCommand>
{
    public SendClubMessageValidator()
    {
        RuleFor(command => command.Body)
            .NotEmpty().WithErrorCode("clubs.message_required")
            .MaximumLength(2000).WithErrorCode("clubs.message_too_long");
    }
}

public sealed class ListClubsHandler(
    IClubRepository clubs,
    IClubAccessPolicy access,
    ICurrentUser currentUser)
{
    public async Task<Result<IReadOnlyList<ClubListItem>>> Handle(
        bool canManage,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Error.Unauthorized("common.unauthorized");
        }

        var items = new List<ClubListItem>();
        foreach (var club in await clubs.ListAsync(canManage, cancellationToken))
        {
            if (!canManage && !await access.CanAccessSubjectAsync(userId, club.SubjectId, cancellationToken))
            {
                continue;
            }

            items.Add(ToListItem(club));
        }

        return items;
    }

    internal static ClubListItem ToListItem(Club club)
        => new(
            club.Id,
            club.SubjectId,
            club.Subject.Name,
            club.Name,
            club.Description,
            club.IsActive,
            0,
            club.Rooms.Where(room => room.IsActive)
                .OrderBy(room => room.SortOrder)
                .Select(room => new ClubRoomItem(room.Id, room.Name, room.Type, room.SortOrder))
                .ToList());
}

public sealed class GetClubHandler(
    IClubRepository clubs,
    IClubAccessPolicy access,
    ICurrentUser currentUser)
{
    public async Task<Result<ClubListItem>> Handle(
        Guid clubId,
        bool canManage,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Error.Unauthorized("common.unauthorized");
        }

        var club = await clubs.FindByIdAsync(clubId, true, cancellationToken);
        if (club is null || (!club.IsActive && !canManage))
        {
            return ClubErrors.ClubNotFound;
        }

        if (!canManage && !await access.CanAccessSubjectAsync(userId, club.SubjectId, cancellationToken))
        {
            return ClubErrors.PackageRequired;
        }

        return ListClubsHandler.ToListItem(club);
    }
}

public sealed class UpdateClubHandler(IClubRepository clubs, IUnitOfWork unitOfWork)
{
    public async Task<Result> Handle(Guid clubId, UpdateClubCommand command, CancellationToken cancellationToken)
    {
        var club = await clubs.FindByIdAsync(clubId, false, cancellationToken);
        if (club is null)
        {
            return ClubErrors.ClubNotFound;
        }

        club.Update(command.Name, command.Description);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

public sealed class SetClubStatusHandler(IClubRepository clubs, IUnitOfWork unitOfWork)
{
    public async Task<Result> Handle(Guid clubId, bool open, CancellationToken cancellationToken)
    {
        var club = await clubs.FindByIdAsync(clubId, false, cancellationToken);
        if (club is null)
        {
            return ClubErrors.ClubNotFound;
        }

        if (open && club.IsActive)
        {
            return ClubErrors.AlreadyOpen;
        }

        if (!open && !club.IsActive)
        {
            return ClubErrors.AlreadyClosed;
        }

        if (open)
        {
            club.Open();
        }
        else
        {
            club.Close();
        }
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

public sealed class AddClubRoomHandler(IClubRepository clubs, IUnitOfWork unitOfWork)
{
    public async Task<Result<ClubRoomItem>> Handle(
        Guid clubId,
        AddClubRoomCommand command,
        CancellationToken cancellationToken)
    {
        var club = await clubs.FindByIdAsync(clubId, true, cancellationToken);
        if (club is null)
        {
            return ClubErrors.ClubNotFound;
        }

        if (club.Rooms.Any(room => string.Equals(
                room.Name,
                command.Name.Trim(),
                StringComparison.OrdinalIgnoreCase)))
        {
            return ClubErrors.RoomAlreadyExists;
        }

        var room = club.AddRoom(command.Name, command.Type, command.SortOrder);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new ClubRoomItem(room.Id, room.Name, room.Type, room.SortOrder);
    }
}

public sealed class ListClubMessagesHandler(
    IClubRepository clubs,
    IClubAccessPolicy access,
    ICurrentUser currentUser)
{
    public async Task<Result<IReadOnlyList<ClubMessageItem>>> Handle(
        Guid roomId,
        int limit,
        bool canManage,
        CancellationToken cancellationToken)
    {
        var roomResult = await GetAccessibleRoom(roomId, canManage, cancellationToken);
        if (roomResult.IsFailure)
        {
            return Result<IReadOnlyList<ClubMessageItem>>.Failure(roomResult.Error);
        }

        var messages = await clubs.ListMessagesAsync(roomId, Math.Clamp(limit, 1, 100), cancellationToken);
        return messages.Select(ToMessageItem).ToList();
    }

    internal async Task<Result<ClubRoom>> GetAccessibleRoom(
        Guid roomId,
        bool canManage,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Error.Unauthorized("common.unauthorized");
        }

        var room = await clubs.FindRoomAsync(roomId, cancellationToken);
        if (room is null || !room.IsActive || (!room.Club.IsActive && !canManage))
        {
            return ClubErrors.RoomNotFound;
        }
        if (!canManage && !await access.CanAccessSubjectAsync(userId, room.Club.SubjectId, cancellationToken))
        {
            return ClubErrors.PackageRequired;
        }

        return room;
    }

    internal static ClubMessageItem ToMessageItem(ClubMessage message)
        => new(
            message.Id,
            message.RoomId,
            message.AuthorUserId,
            $"{message.AuthorUser.FirstName} {message.AuthorUser.LastName}".Trim(),
            message.Body,
            message.CreatedAt);
}

public sealed class SendClubMessageHandler(
    IClubRepository clubs,
    IClubAccessPolicy access,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork)
{
    public async Task<Result<ClubMessageItem>> Handle(
        Guid roomId,
        SendClubMessageCommand command,
        bool canManage,
        CancellationToken cancellationToken)
    {
        var roomAccess = new ListClubMessagesHandler(clubs, access, currentUser);
        var roomResult = await roomAccess.GetAccessibleRoom(roomId, canManage, cancellationToken);
        if (roomResult.IsFailure)
        {
            return Result<ClubMessageItem>.Failure(roomResult.Error);
        }

        if (roomResult.Value.Type != ClubRoomType.Text)
        {
            return ClubErrors.TextRoomRequired;
        }

        var message = ClubMessage.Create(roomId, currentUser.UserId!.Value, command.Body);
        clubs.AddMessage(message);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var saved = (await clubs.ListMessagesAsync(roomId, 1, cancellationToken)).Single();
        return ListClubMessagesHandler.ToMessageItem(saved);
    }
}
