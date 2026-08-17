using Learnier.Application.Common.Abstractions;
using Learnier.Application.Common.Security;
using Learnier.Application.Features.Subscriptions;
using Learnier.Application.Features.Subscriptions.Commands.CreateSubscription;
using Learnier.WebApi.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Learnier.WebApi.Controllers;

[ApiController]
[Route("api/v1/subscriptions")]
[Authorize]
public sealed class SubscriptionsController : ControllerBase
{
    /// <summary>
    /// Katalogdaki bir fiyat surumunden abonelik acar.
    /// </summary>
    /// <remarks>
    /// <c>demo-purchases</c> ucundan farki: plan uretilmez, yoneticinin satisa
    /// actigi hazir plan satin alinir. Ilk donemin ders haklari plan hak
    /// tanimlarindan deftere yazilir.
    /// </remarks>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CreateSubscriptionResult>> CreateSubscription(
        CreateSubscriptionCommand command,
        [FromServices] CreateSubscriptionHandler handler,
        CancellationToken cancellationToken)
        => (await handler.Handle(command, cancellationToken)).ToActionResult(this);

    [HttpPost("demo-purchases")]
    public async Task<ActionResult<PurchaseDemoPackageResult>> PurchaseDemoPackage(
        PurchaseDemoPackageCommand command,
        [FromServices] PurchaseDemoPackageHandler handler,
        CancellationToken cancellationToken)
        => (await handler.Handle(command, cancellationToken)).ToActionResult(this);

    [HttpGet("me/active-packages")]
    public async Task<ActionResult<IReadOnlyList<ActivePackageAccess>>> GetMyActivePackages(
        [FromServices] GetMyActivePackagesHandler handler,
        CancellationToken cancellationToken)
        => (await handler.Handle(cancellationToken)).ToActionResult(this);

    /// <summary>
    /// Ders hakki defterinin hareket gecmisi.
    /// </summary>
    /// <remarks>
    /// <c>me/active-packages</c> kalan hakkin <b>sonucunu</b> verir; bu uc o sonucun
    /// nasil olustugunu gosterir. Parametre verilmezse istegi yapanin defteri doner;
    /// baskasininki icin <c>subscription.manage</c> gerekir.
    /// </remarks>
    [HttpGet("credits/ledger")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<CreditLedgerItem>>> GetCreditLedger(
        [FromServices] ListCreditLedgerHandler handler,
        [FromServices] IAuthorizationService authorization,
        CancellationToken cancellationToken,
        [FromQuery] Guid? learnerUserId = null,
        [FromQuery] Guid? subscriptionId = null)
    {
        ArgumentNullException.ThrowIfNull(handler);
        ArgumentNullException.ThrowIfNull(authorization);

        var canViewOthers = (await authorization.AuthorizeAsync(
            User, Permissions.Subscription.Manage)).Succeeded;

        var result = await handler.Handle(
            learnerUserId, subscriptionId, canViewOthers, cancellationToken);

        return result.ToActionResult(this);
    }
}
