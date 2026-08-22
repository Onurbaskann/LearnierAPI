using Learnier.Application.Common.Security;
using Learnier.Application.Features.Clubs;
using Learnier.Application.Features.Clubs.Commands.CreateClub;
using Learnier.WebApi.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Learnier.WebApi.Controllers;

[ApiController]
[Route("api/v1/clubs")]
[Authorize]
public sealed class ClubsController : ControllerBase
{
    [HttpPost]
    [Authorize(Policy = Permissions.Club.Manage)]
    public async Task<ActionResult<CreateClubResult>> Create(
        CreateClubCommand command,
        [FromServices] CreateClubHandler handler,
        CancellationToken cancellationToken)
        => (await handler.Handle(command, cancellationToken)).ToActionResult(this);

    [HttpGet]
    [Authorize(Policy = Permissions.Club.Read)]
    public async Task<ActionResult<IReadOnlyList<ClubListItem>>> List(
        [FromServices] ListClubsHandler handler,
        [FromServices] IAuthorizationService authorization,
        CancellationToken cancellationToken)
        => (await handler.Handle(await CanManage(authorization), cancellationToken)).ToActionResult(this);

    [HttpGet("{clubId:guid}")]
    [Authorize(Policy = Permissions.Club.Read)]
    public async Task<ActionResult<ClubListItem>> Get(
        Guid clubId,
        [FromServices] GetClubHandler handler,
        [FromServices] IAuthorizationService authorization,
        CancellationToken cancellationToken)
        => (await handler.Handle(clubId, await CanManage(authorization), cancellationToken))
            .ToActionResult(this);

    [HttpPatch("{clubId:guid}")]
    [Authorize(Policy = Permissions.Club.Manage)]
    public async Task<ActionResult> Update(
        Guid clubId,
        UpdateClubCommand command,
        [FromServices] UpdateClubHandler handler,
        CancellationToken cancellationToken)
        => (await handler.Handle(clubId, command, cancellationToken)).ToActionResult(this);

    [HttpPost("{clubId:guid}/close")]
    [Authorize(Policy = Permissions.Club.Manage)]
    public async Task<ActionResult> Close(
        Guid clubId,
        [FromServices] SetClubStatusHandler handler,
        CancellationToken cancellationToken)
        => (await handler.Handle(clubId, false, cancellationToken)).ToActionResult(this);

    [HttpPost("{clubId:guid}/open")]
    [Authorize(Policy = Permissions.Club.Manage)]
    public async Task<ActionResult> Open(
        Guid clubId,
        [FromServices] SetClubStatusHandler handler,
        CancellationToken cancellationToken)
        => (await handler.Handle(clubId, true, cancellationToken)).ToActionResult(this);

    [HttpPost("{clubId:guid}/rooms")]
    [Authorize(Policy = Permissions.Club.Manage)]
    public async Task<ActionResult<ClubRoomItem>> AddRoom(
        Guid clubId,
        AddClubRoomCommand command,
        [FromServices] AddClubRoomHandler handler,
        CancellationToken cancellationToken)
        => (await handler.Handle(clubId, command, cancellationToken)).ToActionResult(this);

    [HttpGet("rooms/{roomId:guid}/messages")]
    [Authorize(Policy = Permissions.Club.Read)]
    public async Task<ActionResult<IReadOnlyList<ClubMessageItem>>> ListMessages(
        Guid roomId,
        [FromServices] ListClubMessagesHandler handler,
        [FromServices] IAuthorizationService authorization,
        CancellationToken cancellationToken,
        [FromQuery] int limit = 50)
        => (await handler.Handle(
                roomId,
                limit,
                await CanManage(authorization),
                cancellationToken))
            .ToActionResult(this);

    [HttpPost("rooms/{roomId:guid}/messages")]
    [Authorize(Policy = Permissions.Club.MessageSend)]
    public async Task<ActionResult<ClubMessageItem>> SendMessage(
        Guid roomId,
        SendClubMessageCommand command,
        [FromServices] SendClubMessageHandler handler,
        [FromServices] IAuthorizationService authorization,
        CancellationToken cancellationToken)
        => (await handler.Handle(
                roomId,
                command,
                await CanManage(authorization),
                cancellationToken))
            .ToActionResult(this);

    private async Task<bool> CanManage(IAuthorizationService authorization)
        => (await authorization.AuthorizeAsync(User, Permissions.Club.Manage)).Succeeded;
}
