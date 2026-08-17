using Learnier.Application.Features.Friends;
using Learnier.WebApi.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Learnier.WebApi.Controllers;

[ApiController]
[Route("api/v1/friends")]
[Authorize]
public sealed class FriendsController : ControllerBase
{
    [HttpGet("search")]
    public async Task<ActionResult<IReadOnlyList<FriendUserSearchItem>>> SearchUsers(
        [FromQuery] SearchUsersQuery request,
        [FromServices] SearchUsersHandler handler,
        CancellationToken cancellationToken)
        => (await handler.Handle(request, cancellationToken)).ToActionResult(this);

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<FriendListItem>>> List(
        [FromServices] ListFriendsHandler handler,
        CancellationToken cancellationToken)
        => (await handler.Handle(cancellationToken)).ToActionResult(this);

    [HttpGet("requests/incoming")]
    public async Task<ActionResult<IReadOnlyList<FriendRequestListItem>>> ListIncoming(
        [FromServices] ListIncomingFriendRequestsHandler handler,
        CancellationToken cancellationToken)
        => (await handler.Handle(cancellationToken)).ToActionResult(this);

    [HttpGet("requests/sent")]
    public async Task<ActionResult<IReadOnlyList<FriendRequestListItem>>> ListSent(
        [FromServices] ListSentFriendRequestsHandler handler,
        CancellationToken cancellationToken)
        => (await handler.Handle(cancellationToken)).ToActionResult(this);

    [HttpPost("requests")]
    public async Task<ActionResult<FriendRequestListItem>> Send(
        SendFriendRequestCommand command,
        [FromServices] SendFriendRequestHandler handler,
        CancellationToken cancellationToken)
        => (await handler.Handle(command, cancellationToken)).ToActionResult(this);

    [HttpPost("requests/{requestId:guid}/accept")]
    public async Task<ActionResult<FriendListItem>> Accept(
        Guid requestId,
        [FromServices] AcceptFriendRequestHandler handler,
        CancellationToken cancellationToken)
        => (await handler.Handle(requestId, cancellationToken)).ToActionResult(this);

    [HttpPost("requests/{requestId:guid}/decline")]
    public async Task<ActionResult> Decline(
        Guid requestId,
        [FromServices] DeclineFriendRequestHandler handler,
        CancellationToken cancellationToken)
        => (await handler.Handle(requestId, cancellationToken)).ToActionResult(this);

    [HttpDelete("requests/{requestId:guid}")]
    public async Task<ActionResult> CancelSentRequest(
        Guid requestId,
        [FromServices] CancelSentFriendRequestHandler handler,
        CancellationToken cancellationToken)
        => (await handler.Handle(requestId, cancellationToken)).ToActionResult(this);

    [HttpDelete("{friendshipId:guid}")]
    public async Task<ActionResult> RemoveFriend(
        Guid friendshipId,
        [FromServices] RemoveFriendHandler handler,
        CancellationToken cancellationToken)
        => (await handler.Handle(friendshipId, cancellationToken)).ToActionResult(this);
}
