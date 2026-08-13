using Learnier.Application.Common.Security;
using Learnier.Application.Features.Compensation;
using Learnier.WebApi.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Learnier.WebApi.Controllers;

[ApiController]
[Route("api/v1/admin/compensation")]
[Authorize(Policy = Permissions.Compensation.Manage)]
public sealed class CompensationController : ControllerBase
{
    [HttpPut("rates")]
    public async Task<ActionResult<ConfigureCompensationRateResult>> ConfigureRate(
        ConfigureCompensationRateCommand command,
        [FromServices] ConfigureCompensationRateHandler handler,
        CancellationToken cancellationToken)
        => (await handler.Handle(command, cancellationToken)).ToActionResult(this);

    [HttpPut("penalty-steps")]
    public async Task<ActionResult> ConfigurePenaltySteps(
        ConfigurePenaltyStepsCommand command,
        [FromServices] ConfigurePenaltyStepsHandler handler,
        CancellationToken cancellationToken)
        => (await handler.Handle(command, cancellationToken)).ToActionResult(this);
}
