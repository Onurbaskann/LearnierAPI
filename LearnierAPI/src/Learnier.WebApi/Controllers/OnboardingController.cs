using Learnier.Application.Features.Onboarding;
using Learnier.WebApi.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Learnier.WebApi.Controllers;

[ApiController]
[Route("api/v1/onboarding")]
[Authorize]
public sealed class OnboardingController : ControllerBase
{
    [HttpGet("me")]
    public async Task<ActionResult<IReadOnlyList<LearnerOnboardingResult>>> GetMine(
        [FromServices] GetMyLearnerOnboardingHandler handler,
        CancellationToken cancellationToken)
        => (await handler.Handle(cancellationToken)).ToActionResult(this);

    [HttpPut("me")]
    public async Task<ActionResult<LearnerOnboardingResult>> Save(
        SaveLearnerOnboardingCommand command,
        [FromServices] SaveLearnerOnboardingHandler handler,
        CancellationToken cancellationToken)
        => (await handler.Handle(command, cancellationToken)).ToActionResult(this);
}
