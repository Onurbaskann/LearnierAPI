using Learnier.Application.Features.Accounts;
using Learnier.Application.Features.Accounts.Commands.UpdateMyContact;
using Learnier.Application.Features.Accounts.Queries.GetMyContact;
using Learnier.WebApi.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Learnier.WebApi.Controllers;

[ApiController]
[Route("api/v1/account")]
[Authorize]
public sealed class AccountController : ControllerBase
{
    [HttpGet("contact")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AccountContact>> GetContact(
        [FromServices] GetMyContactHandler handler,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return (await handler.Handle(cancellationToken)).ToActionResult(this);
    }

    [HttpPut("contact")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AccountContact>> UpdateContact(
        UpdateMyContactCommand command,
        [FromServices] UpdateMyContactHandler handler,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return (await handler.Handle(command, cancellationToken)).ToActionResult(this);
    }
}
