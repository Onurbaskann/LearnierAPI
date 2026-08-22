using Learnier.Application.Common.Abstractions;
using Learnier.Application.Features.Billing.Queries;
using Learnier.WebApi.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Learnier.WebApi.Controllers;

/// <summary>
/// Ogrencinin satin alma karari icin gordugu katalog.
/// </summary>
/// <remarks>
/// <see cref="PlansController"/> yonetim tarafidir ve <c>subscription.manage</c>
/// ister; ogrenci onu kullanamaz. Bu uc izin istemez ama yalnizca satisa acilmis
/// planlari gosterir: taslak, emekli ve satin alma akisinin ortuk urettigi planlar
/// listeye girmez.
/// </remarks>
[ApiController]
[Route("api/v1/catalog")]
[Authorize]
public sealed class CatalogController : ControllerBase
{
    /// <summary>Satin alinabilecek aktif planlar.</summary>
    [HttpGet("plans")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<CatalogPlanItem>>> ListPlans(
        [FromServices] ListPurchasablePlansHandler handler,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(handler);

        return (await handler.Handle(cancellationToken)).ToActionResult(this);
    }
}
