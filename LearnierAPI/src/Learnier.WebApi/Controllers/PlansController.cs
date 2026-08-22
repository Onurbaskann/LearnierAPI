using Learnier.Application.Common.Abstractions;
using Learnier.Application.Common.Security;
using Learnier.Application.Features.Billing.Commands.ActivatePlan;
using Learnier.Application.Features.Billing.Commands.AddPlanEntitlement;
using Learnier.Application.Features.Billing.Commands.AddPlanPrice;
using Learnier.Application.Features.Billing.Commands.CreatePlan;
using Learnier.Application.Features.Billing.Commands.GrantPlanAccess;
using Learnier.Application.Features.Billing.Queries;
using Learnier.Domain.Billing;
using Learnier.Domain.Scheduling;
using Learnier.WebApi.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Learnier.WebApi.Controllers;

/// <summary>
/// Abonelik planlarinin yonetimi.
/// </summary>
/// <remarks>
/// Yonetici tarafi burada, ogrencinin satin alma ve bakiye uclari
/// <see cref="SubscriptionsController"/> icinde. Ayrim izin sinirini takip eder:
/// buradaki her uc <c>subscription.manage</c> ister.
/// </remarks>
[ApiController]
[Route("api/v1/plans")]
[Authorize(Policy = Permissions.Subscription.Manage)]
public sealed class PlansController : ControllerBase
{
    /// <summary>Kurumun butun planlari - taslak ve emekli olanlar dahil.</summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<PlanDetail>>> List(
        [FromServices] ListPlansHandler handler,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(handler);

        return (await handler.Handle(cancellationToken)).ToActionResult(this);
    }

    /// <summary>Tek planin ayrintisi: fiyat gecmisi, haklar ve kapsam.</summary>
    [HttpGet("{planId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PlanDetail>> Get(
        Guid planId,
        [FromServices] GetPlanHandler handler,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(handler);

        return (await handler.Handle(planId, cancellationToken)).ToActionResult(this);
    }

    /// <summary>Yeni abonelik plani olusturur. Plan taslak baslar.</summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<CreatePlanResult>> Create(
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
    /// Mevcut fiyat guncellenmez, arsivlenir; yanitta arsivlenenin kimligi doner.
    /// Boylece eski aboneliklerin hangi tutardan satildigi izlenebilir kalir.
    /// </remarks>
    [HttpPost("{planId:guid}/prices")]
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
    [HttpPost("{planId:guid}/entitlements")]
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
                request.Quantity,
                request.LessonDurationMinutes),
            cancellationToken);

        return result.ToActionResult(this);
    }

    /// <summary>Kisitli kapsamli plana alan veya egitim erisimi ekler.</summary>
    [HttpPost("{planId:guid}/access")]
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

    /// <summary>Plani satisa acar.</summary>
    /// <remarks>
    /// Aktif fiyati ve en az bir hak tanimi olmayan plan acilamaz: taslak plan
    /// musteriye hicbir sey vermeyecegi icin satisa cikmamali.
    /// </remarks>
    [HttpPost("{planId:guid}/activate")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> Activate(
        Guid planId,
        [FromServices] ActivatePlanHandler handler,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(handler);

        var result = await handler.Handle(planId, cancellationToken);

        return result.ToActionResult(this);
    }
}

/// <summary>Plan kimligi rotadan geldigi icin govdede tasinmaz.</summary>
public sealed record AddPlanPriceRequest(
    string Currency,
    decimal Amount,
    BillingInterval BillingInterval,
    int BillingIntervalCount);

/// <param name="LessonDurationMinutes">
/// Birebir ders kredisinde zorunlu: 30 veya 50. Diger haklarda bos birakilir.
/// </param>
public sealed record AddPlanEntitlementRequest(
    EntitlementType EntitlementType,
    SessionType SessionType,
    EntitlementResetPeriod ResetPeriod,
    int? Quantity,
    int? LessonDurationMinutes);

/// <param name="SubjectId">Alanin tamamini kapsamak icin.</param>
/// <param name="CourseId">Yalnizca bir egitimi kapsamak icin.</param>
public sealed record GrantPlanAccessRequest(Guid? SubjectId, Guid? CourseId);
