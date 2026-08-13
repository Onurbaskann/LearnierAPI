using Learnier.Application.Common.Abstractions;
using Learnier.Application.Features.Subscriptions;
using Learnier.WebApi.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Learnier.WebApi.Controllers;

[ApiController]
[Route("api/v1/subscriptions")]
[Authorize]
public sealed class SubscriptionsController : ControllerBase
{
    [HttpGet("me/active-packages")]
    public async Task<ActionResult<IReadOnlyList<ActivePackageAccess>>> GetMyActivePackages(
        [FromServices] GetMyActivePackagesHandler handler,
        CancellationToken cancellationToken)
        => (await handler.Handle(cancellationToken)).ToActionResult(this);
}
