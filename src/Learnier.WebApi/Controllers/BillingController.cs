using Learnier.Application.Common.Security;
using Learnier.Application.Features.Billing.Commands.ActivatePlan;
using Learnier.Application.Features.Billing.Commands.AddPlanEntitlement;
using Learnier.Application.Features.Billing.Commands.AddPlanPrice;
using Learnier.Application.Features.Billing.Commands.CreatePlan;
using Learnier.Application.Features.Billing.Commands.CreateSubscription;
using Learnier.Application.Features.Billing.Commands.GrantPlanAccess;
using Learnier.Application.Features.Billing.Queries;
using Learnier.Domain.Billing;
using Learnier.Domain.Scheduling;
using Learnier.WebApi.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Learnier.WebApi.Controllers;

/// <summary>
/// Abonelik planlari, abonelikler ve ders hakki defteri.
/// </summary>
[ApiController]
[Route("api/v1")]
[Authorize]
public sealed class BillingController : ControllerBase
{
    /// <summary>Yeni abonelik plani olusturur. Plan taslak baslar.</summary>
    [HttpPost("plans")]
    [Authorize(Policy = Permissions.Subscription.Manage)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<CreatePlanResult>> CreatePlan(
        CreatePlanCommand command,
        [FromServices] CreatePlanHandler handler,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(handler);

        var result = await handler.Handle(command, cancellationToken);

        return result.ToActionResult(this);
    }

    /// <summary>
    /// Plana yeni fiyat surumu ekler.
    /// </summary>
    /// <remarks>
    /// Mevcut fiyat guncellenmez, arsivlenir. Yanitta arsivlenen fiyatin kimligi doner.
    /// </remarks>
    [HttpPost("plans/{planId:guid}/prices")]
    [Authorize(Policy = Permissions.Subscription.Manage)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AddPlanPriceResult>> AddPrice(
        Guid planId,
        AddPlanPriceRequest request,
        [FromServices] AddPlanPriceHandler handler,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(handler);
        ArgumentNullException.ThrowIfNull(request);

        var result = await handler.Handle(
            new AddPlanPriceCommand(
                planId,
                request.Currency,
                request.Amount,
                request.BillingInterval,
                request.BillingIntervalCount),
            cancellationToken);

        return result.ToActionResult(this);
    }

    /// <summary>Plana hak tanimi ekler.</summary>
    [HttpPost("plans/{planId:guid}/entitlements")]
    [Authorize(Policy = Permissions.Subscription.Manage)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AddPlanEntitlementResult>> AddEntitlement(
        Guid planId,
        AddPlanEntitlementRequest request,
        [FromServices] AddPlanEntitlementHandler handler,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(handler);
        ArgumentNullException.ThrowIfNull(request);

        var result = await handler.Handle(
            new AddPlanEntitlementCommand(
                planId,
                request.EntitlementType,
                request.SessionType,
                request.ResetPeriod,
                request.Quantity),
            cancellationToken);

        return result.ToActionResult(this);
    }

    /// <summary>Plani satisa acar.</summary>
    /// <remarks>
    /// Aktif fiyati ve en az bir hak tanimi olmayan plan acilamaz.
    /// </remarks>
    [HttpPost("plans/{planId:guid}/activate")]
    [Authorize(Policy = Permissions.Subscription.Manage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> ActivatePlan(
        Guid planId,
        [FromServices] ActivatePlanHandler handler,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(handler);

        var result = await handler.Handle(planId, cancellationToken);

        return result.ToActionResult(this);
    }

    /// <summary>Kisitli kapsamli plana alan veya egitim erisimi ekler.</summary>
    [HttpPost("plans/{planId:guid}/access")]
    [Authorize(Policy = Permissions.Subscription.Manage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> GrantAccess(
        Guid planId,
        GrantPlanAccessRequest request,
        [FromServices] GrantPlanAccessHandler handler,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(handler);
        ArgumentNullException.ThrowIfNull(request);

        var result = await handler.Handle(
            new GrantPlanAccessCommand(planId, request.SubjectId, request.CourseId),
            cancellationToken);

        return result.ToActionResult(this);
    }

    /// <summary>
    /// Abonelik acar ve ilk donemin ders haklarini deftere yazar.
    /// </summary>
    [HttpPost("subscriptions")]
    [Authorize(Policy = Permissions.Subscription.Manage)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CreateSubscriptionResult>> CreateSubscription(
        CreateSubscriptionCommand command,
        [FromServices] CreateSubscriptionHandler handler,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(handler);

        var result = await handler.Handle(command, cancellationToken);

        return result.ToActionResult(this);
    }

    /// <summary>
    /// Ders hakki bakiyeleri. Parametre verilmezse istegi yapanin bakiyesi doner.
    /// </summary>
    [HttpGet("credits/balance")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<CreditBalanceItem>>> GetBalance(
        [FromServices] GetCreditBalanceHandler handler,
        [FromServices] IAuthorizationService authorization,
        CancellationToken cancellationToken,
        [FromQuery] Guid? learnerUserId = null)
    {
        ArgumentNullException.ThrowIfNull(handler);

        var result = await handler.Handle(
            learnerUserId,
            await CanViewOthers(authorization),
            cancellationToken);

        return result.ToActionResult(this);
    }

    /// <summary>Bir aboneligin kredi hareketleri.</summary>
    [HttpGet("subscriptions/{subscriptionId:guid}/credits")]
    [Authorize(Policy = Permissions.Subscription.Manage)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<CreditLedgerItem>>> ListLedger(
        Guid subscriptionId,
        [FromServices] ListCreditLedgerHandler handler,
        CancellationToken cancellationToken,
        [FromQuery] Guid learnerUserId = default)
    {
        ArgumentNullException.ThrowIfNull(handler);

        var result = await handler.Handle(subscriptionId, learnerUserId, cancellationToken);

        return result.ToActionResult(this);
    }

    /// <summary>
    /// Cagirici baskasinin bakiyesini gorebilir mi?
    /// </summary>
    /// <remarks>
    /// Yetkisi olmayan reddedilmez, yalnizca kendi bakiyesini gorur.
    /// </remarks>
    private async Task<bool> CanViewOthers(IAuthorizationService authorization)
    {
        ArgumentNullException.ThrowIfNull(authorization);

        var result = await authorization.AuthorizeAsync(User, Permissions.Subscription.Manage);

        return result.Succeeded;
    }
}

/// <summary>Plan kimligi rotadan geldigi icin govdede tasinmaz.</summary>
public sealed record AddPlanPriceRequest(
    string Currency,
    decimal Amount,
    BillingInterval BillingInterval,
    int BillingIntervalCount);

public sealed record AddPlanEntitlementRequest(
    EntitlementType EntitlementType,
    SessionType SessionType,
    EntitlementResetPeriod ResetPeriod,
    int? Quantity);

/// <param name="SubjectId">Alanin tamamini kapsamak icin.</param>
/// <param name="CourseId">Yalnizca bir egitimi kapsamak icin.</param>
public sealed record GrantPlanAccessRequest(Guid? SubjectId, Guid? CourseId);
