using Learnier.Application.Common.Abstractions;
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
    [HttpGet("settings")]
    public async Task<ActionResult<CompensationSettings>> GetSettings(
        [FromServices] GetCompensationSettingsHandler handler,
        CancellationToken cancellationToken)
        => (await handler.Handle(cancellationToken)).ToActionResult(this);

    [HttpGet("instructors/{instructorProfileId:guid}/penalties")]
    public async Task<ActionResult<InstructorPenaltyHistory>> GetInstructorPenalties(
        Guid instructorProfileId,
        [FromServices] GetInstructorPenaltyHistoryHandler handler,
        CancellationToken cancellationToken)
        => (await handler.Handle(instructorProfileId, cancellationToken)).ToActionResult(this);

    [HttpPost("instructors/{instructorProfileId:guid}/penalties/waive")]
    public async Task<ActionResult> WaiveInstructorPenalty(
        Guid instructorProfileId,
        WaivePenaltyRequest request,
        [FromServices] WaiveInstructorPenaltyHandler handler,
        CancellationToken cancellationToken)
        => (await handler.Handle(
            new WaiveInstructorPenaltyCommand(instructorProfileId, request.Reason),
            cancellationToken)).ToActionResult(this);

    [HttpGet("cancellation-policy")]
    public async Task<ActionResult<CancellationPolicyDto>> GetCancellationPolicy(
        [FromServices] GetCancellationPolicyHandler handler,
        CancellationToken cancellationToken)
        => (await handler.Handle(cancellationToken)).ToActionResult(this);

    [HttpPut("cancellation-policy")]
    public async Task<ActionResult<CancellationPolicyDto>> ConfigureCancellationPolicy(
        ConfigureCancellationPolicyCommand command,
        [FromServices] ConfigureCancellationPolicyHandler handler,
        CancellationToken cancellationToken)
        => (await handler.Handle(command, cancellationToken)).ToActionResult(this);

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

public sealed record WaivePenaltyRequest(string Reason);
