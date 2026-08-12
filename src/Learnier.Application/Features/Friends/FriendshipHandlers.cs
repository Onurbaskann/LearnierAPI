using FluentValidation;
using Learnier.Application.Common.Abstractions;
using Learnier.Application.Common.Results;
using Learnier.Domain.Social;

namespace Learnier.Application.Features.Friends;

public sealed record FriendListItem(
    Guid FriendshipId,
    Guid UserId,
    string Email,
    string FirstName,
    string LastName,
    DateTimeOffset FriendsSince);

public sealed record FriendRequestListItem(
    Guid RequestId,
    Guid UserId,
    string Email,
    string FirstName,
    string LastName,
    DateTimeOffset RequestedAt);

public sealed record SendFriendRequestCommand(Guid UserId);

public sealed record SearchUsersQuery(string Query);

public enum FriendshipRelationState
{
    None,
    IncomingRequest,
    SentRequest,
    Friends
}

public sealed record FriendUserSearchItem(
    Guid UserId,
    string Email,
    string FirstName,
    string LastName,
    Guid? FriendshipId,
    FriendshipRelationState RelationState);

internal sealed class SendFriendRequestValidator : AbstractValidator<SendFriendRequestCommand>
{
    public SendFriendRequestValidator()
    {
        RuleFor(command => command.UserId)
            .NotEmpty().WithErrorCode("friends.user_id_required");
    }
}

internal sealed class SearchUsersValidator : AbstractValidator<SearchUsersQuery>
{
    public SearchUsersValidator()
    {
        RuleFor(query => query.Query)
            .NotEmpty().WithErrorCode("friends.search_term_required")
            .MinimumLength(2).WithErrorCode("friends.search_term_too_short")
            .MaximumLength(100).WithErrorCode("friends.search_term_too_long");
    }
}

public sealed class SearchUsersHandler(IFriendshipRepository friendships, ICurrentUser currentUser)
{
    private const int ResultLimit = 20;

    public async Task<Result<IReadOnlyList<FriendUserSearchItem>>> Handle(
        SearchUsersQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (currentUser.UserId is not { } userId)
        {
            return Error.Unauthorized("common.unauthorized");
        }

        var users = await friendships.SearchUsersAsync(
            userId,
            query.Query,
            ResultLimit,
            cancellationToken);

        return users.Select(user => ToSearchItem(user, userId)).ToList();
    }

    private static FriendUserSearchItem ToSearchItem(FriendshipSearchPeer user, Guid currentUserId)
    {
        var relationState = ToRelationState(user, currentUserId);
        return new FriendUserSearchItem(
            user.UserId,
            user.Email,
            user.FirstName,
            user.LastName,
            relationState is FriendshipRelationState.None ? null : user.FriendshipId,
            relationState);
    }

    private static FriendshipRelationState ToRelationState(FriendshipSearchPeer user, Guid currentUserId)
        => user.FriendshipStatus switch
        {
            FriendshipStatus.Accepted => FriendshipRelationState.Friends,
            FriendshipStatus.Pending when user.RequestedByUserId == currentUserId
                => FriendshipRelationState.SentRequest,
            FriendshipStatus.Pending => FriendshipRelationState.IncomingRequest,
            _ => FriendshipRelationState.None
        };
}

public sealed class ListFriendsHandler(IFriendshipRepository friendships, ICurrentUser currentUser)
{
    public async Task<Result<IReadOnlyList<FriendListItem>>> Handle(CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Error.Unauthorized("common.unauthorized");
        }
        var peers = await friendships.ListFriendsAsync(userId, cancellationToken);
        return peers.Select(ToFriend).ToList();
    }

    internal static FriendListItem ToFriend(FriendshipPeer peer)
        => new(peer.FriendshipId, peer.UserId, peer.Email, peer.FirstName, peer.LastName, peer.ChangedAt);
}

public sealed class ListIncomingFriendRequestsHandler(IFriendshipRepository friendships, ICurrentUser currentUser)
{
    public async Task<Result<IReadOnlyList<FriendRequestListItem>>> Handle(CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Error.Unauthorized("common.unauthorized");
        }
        var peers = await friendships.ListIncomingRequestsAsync(userId, cancellationToken);
        return peers.Select(ToRequest).ToList();
    }

    internal static FriendRequestListItem ToRequest(FriendshipPeer peer)
        => new(peer.FriendshipId, peer.UserId, peer.Email, peer.FirstName, peer.LastName, peer.ChangedAt);
}

public sealed class ListSentFriendRequestsHandler(IFriendshipRepository friendships, ICurrentUser currentUser)
{
    public async Task<Result<IReadOnlyList<FriendRequestListItem>>> Handle(CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Error.Unauthorized("common.unauthorized");
        }
        var peers = await friendships.ListSentRequestsAsync(userId, cancellationToken);
        return peers.Select(ListIncomingFriendRequestsHandler.ToRequest).ToList();
    }
}

public sealed class SendFriendRequestHandler(
    IFriendshipRepository friendships,
    IUserRepository users,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    public async Task<Result<FriendRequestListItem>> Handle(
        SendFriendRequestCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (currentUser.UserId is not { } userId)
        {
            return Error.Unauthorized("common.unauthorized");
        }

        var target = await users.FindByIdAsync(command.UserId, cancellationToken);
        if (target is null)
        {
            return FriendshipErrors.UserNotFound;
        }

        if (target.Id == userId)
        {
            return FriendshipErrors.CannotAddSelf;
        }

        var now = clock.UtcNow;
        var friendship = await friendships.FindBetweenAsync(userId, target.Id, cancellationToken);
        if (friendship?.Status is FriendshipStatus.Accepted)
        {
            return FriendshipErrors.AlreadyFriends;
        }

        if (friendship?.Status is FriendshipStatus.Pending)
        {
            return FriendshipErrors.RequestAlreadyPending;
        }

        if (friendship is null)
        {
            friendship = Friendship.Request(userId, target.Id, now);
            friendships.Add(friendship);
        }
        else
        {
            friendship.RequestAgain(userId, now);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new FriendRequestListItem(
            friendship.Id, target.Id, target.Email, target.FirstName, target.LastName, friendship.RequestedAt);
    }
}

public sealed class AcceptFriendRequestHandler(
    IFriendshipRepository friendships,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    public async Task<Result<FriendListItem>> Handle(Guid requestId, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Error.Unauthorized("common.unauthorized");
        }

        var friendship = await friendships.FindByIdAsync(requestId, cancellationToken);
        if (friendship is null || friendship.Status is not FriendshipStatus.Pending)
        {
            return FriendshipErrors.RequestNotFound;
        }

        if (!friendship.Includes(userId) || friendship.RequestedByUserId == userId)
        {
            return FriendshipErrors.RequestNotOwned;
        }

        friendship.Accept(userId, clock.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var peer = await friendships.FindPeerAsync(friendship.Id, userId, cancellationToken);
        return peer is null ? FriendshipErrors.RequestNotFound : ListFriendsHandler.ToFriend(peer);
    }
}

public sealed class DeclineFriendRequestHandler(
    IFriendshipRepository friendships,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    public async Task<Result> Handle(Guid requestId, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Error.Unauthorized("common.unauthorized");
        }

        var friendship = await friendships.FindByIdAsync(requestId, cancellationToken);
        if (friendship is null || friendship.Status is not FriendshipStatus.Pending)
        {
            return FriendshipErrors.RequestNotFound;
        }

        if (!friendship.Includes(userId) || friendship.RequestedByUserId == userId)
        {
            return FriendshipErrors.RequestNotOwned;
        }

        friendship.Decline(userId, clock.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

public sealed class CancelSentFriendRequestHandler(
    IFriendshipRepository friendships,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork)
{
    public async Task<Result> Handle(Guid requestId, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Error.Unauthorized("common.unauthorized");
        }

        var friendship = await friendships.FindByIdAsync(requestId, cancellationToken);
        if (friendship is null || friendship.Status is not FriendshipStatus.Pending)
        {
            return FriendshipErrors.RequestNotFound;
        }

        if (!friendship.Includes(userId) || friendship.RequestedByUserId != userId)
        {
            return FriendshipErrors.RequestNotOwned;
        }

        friendships.Remove(friendship);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

public sealed class RemoveFriendHandler(
    IFriendshipRepository friendships,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork)
{
    public async Task<Result> Handle(Guid friendshipId, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Error.Unauthorized("common.unauthorized");
        }

        var friendship = await friendships.FindByIdAsync(friendshipId, cancellationToken);
        if (friendship is null || friendship.Status is not FriendshipStatus.Accepted)
        {
            return FriendshipErrors.FriendshipNotFound;
        }

        if (!friendship.Includes(userId))
        {
            return FriendshipErrors.FriendshipNotOwned;
        }

        friendships.Remove(friendship);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
