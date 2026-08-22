using Learnier.Application.Features.Scheduling.Queries;
using Learnier.Domain.Scheduling;
using Learnier.WebApi.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Learnier.WebApi.Controllers;

[ApiController]
[Route("api/v1/meetings/sandbox")]
[Authorize]
public sealed class MeetingsController : ControllerBase
{
    /// <summary>Aktif rezervasyonu bulunan ogrenciyi sandbox ders odasina alir.</summary>
    [HttpGet("{meetingId:guid}/join")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<MeetingRoomAccessResult>> Join(
        Guid meetingId,
        [FromServices] AccessMeetingHandler handler,
        CancellationToken cancellationToken)
        => (await handler.Handle(
            meetingId,
            MeetingParticipantRole.Attendee,
            cancellationToken)).ToActionResult(this);

    /// <summary>Oturuma atanmis egitmeni sandbox ders odasina host olarak alir.</summary>
    [HttpGet("{meetingId:guid}/host")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<MeetingRoomAccessResult>> Host(
        Guid meetingId,
        [FromServices] AccessMeetingHandler handler,
        CancellationToken cancellationToken)
        => (await handler.Handle(
            meetingId,
            MeetingParticipantRole.Host,
            cancellationToken)).ToActionResult(this);
}
