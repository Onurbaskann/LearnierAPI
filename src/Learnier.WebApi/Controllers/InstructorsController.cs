using Learnier.Application.Common.Models;
using Learnier.Application.Common.Security;
using Learnier.Application.Features.Teaching.Commands.ActivateInstructor;
using Learnier.Application.Features.Teaching.Commands.AddAvailability;
using Learnier.Application.Features.Teaching.Commands.AddAvailabilityOverride;
using Learnier.Application.Features.Teaching.Commands.AddInstructorSubject;
using Learnier.Application.Features.Teaching.Commands.CreateInstructorProfile;
using Learnier.Application.Features.Teaching.Queries;
using Learnier.Domain.Teaching;
using Learnier.WebApi.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Learnier.WebApi.Controllers;

/// <summary>
/// Egitmen profilleri, yetkinlikler ve uygunluk takvimi.
/// </summary>
/// <remarks>
/// Profil olusturma ve onaylama yoneticiye aittir. Yetkinlik ve uygunluk ise
/// egitmenin kendisi tarafindan da yonetilebilir; kural
/// <c>InstructorAccess</c> icinde tanimli.
/// </remarks>
[ApiController]
[Route("api/v1/instructors")]
[Authorize]
public sealed class InstructorsController : ControllerBase
{
    /// <summary>Bir uyelik icin egitmen profili acar. Profil onay bekler durumda baslar.</summary>
    [HttpPost]
    [Authorize(Policy = Permissions.Organization.MemberManage)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CreateInstructorProfileResult>> Create(
        CreateInstructorProfileCommand command,
        [FromServices] CreateInstructorProfileHandler handler,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(handler);

        var result = await handler.Handle(command, cancellationToken);

        return result.ToActionResult(this);
    }

    /// <summary>Egitmen profilini aktiflestirir.</summary>
    /// <remarks>
    /// Yalnizca yonetici: egitmen kendi basvurusunu onaylayamamali.
    /// </remarks>
    [HttpPost("{profileId:guid}/activate")]
    [Authorize(Policy = Permissions.Organization.MemberManage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Activate(
        Guid profileId,
        [FromServices] ActivateInstructorHandler handler,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(handler);

        var result = await handler.Handle(profileId, cancellationToken);

        return result.ToActionResult(this);
    }

    /// <summary>Kurumun egitmenlerini sayfali listeler.</summary>
    /// <param name="subjectId">Verilirse yalnizca o alanda yetkin egitmenler doner.</param>
    [HttpGet]
    [Authorize(Policy = Permissions.Course.Read)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PagedResult<InstructorListItem>>> List(
        [FromServices] ListInstructorsHandler handler,
        CancellationToken cancellationToken,
        [FromQuery] Guid? subjectId = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        ArgumentNullException.ThrowIfNull(handler);

        var result = await handler.Handle(
            new PageRequest { Page = page, PageSize = pageSize },
            subjectId,
            cancellationToken);

        return result.ToActionResult(this);
    }

    /// <summary>Egitmenin yetkinlik ve uygunluklariyla birlikte detayi.</summary>
    [HttpGet("{profileId:guid}")]
    [Authorize(Policy = Permissions.Course.Read)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<InstructorDetail>> GetDetail(
        Guid profileId,
        [FromServices] GetInstructorDetailHandler handler,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(handler);

        var result = await handler.Handle(profileId, cancellationToken);

        return result.ToActionResult(this);
    }

    /// <summary>Egitmene brans yetkinligi ekler.</summary>
    [HttpPost("{profileId:guid}/subjects")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AddInstructorSubjectResult>> AddSubject(
        Guid profileId,
        AddInstructorSubjectRequest request,
        [FromServices] AddInstructorSubjectHandler handler,
        [FromServices] IAuthorizationService authorization,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(handler);
        ArgumentNullException.ThrowIfNull(request);

        var result = await handler.Handle(
            new AddInstructorSubjectCommand(profileId, request.SubjectId, request.LevelId),
            await CanManageInstructors(authorization),
            cancellationToken);

        return result.ToActionResult(this);
    }

    /// <summary>Egitmene haftalik uygunluk araligi ekler.</summary>
    [HttpPost("{profileId:guid}/availabilities")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AddAvailabilityResult>> AddAvailability(
        Guid profileId,
        AddAvailabilityRequest request,
        [FromServices] AddAvailabilityHandler handler,
        [FromServices] IAuthorizationService authorization,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(handler);
        ArgumentNullException.ThrowIfNull(request);

        var result = await handler.Handle(
            new AddAvailabilityCommand(
                profileId,
                request.DayOfWeek,
                request.StartLocalTime,
                request.EndLocalTime,
                request.ValidFrom,
                request.ValidUntil),
            await CanManageInstructors(authorization),
            cancellationToken);

        return result.ToActionResult(this);
    }

    /// <summary>Belirli bir tarih icin uygunluk istisnasi ekler: izin veya ek mesai.</summary>
    [HttpPost("{profileId:guid}/availability-overrides")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AddAvailabilityOverrideResult>> AddOverride(
        Guid profileId,
        AddAvailabilityOverrideRequest request,
        [FromServices] AddAvailabilityOverrideHandler handler,
        [FromServices] IAuthorizationService authorization,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(handler);
        ArgumentNullException.ThrowIfNull(request);

        var result = await handler.Handle(
            new AddAvailabilityOverrideCommand(
                profileId,
                request.OverrideDate,
                request.OverrideType,
                request.StartLocalTime,
                request.EndLocalTime,
                request.Reason),
            await CanManageInstructors(authorization),
            cancellationToken);

        return result.ToActionResult(this);
    }

    /// <summary>Egitmenin verilen tarihten itibaren gecerli uygunluk istisnalari.</summary>
    [HttpGet("{profileId:guid}/availability-overrides")]
    [Authorize(Policy = Permissions.Course.Read)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<AvailabilityOverrideDetail>>> ListOverrides(
        Guid profileId,
        [FromServices] ListAvailabilityOverridesHandler handler,
        CancellationToken cancellationToken,
        [FromQuery] DateOnly? from = null)
    {
        ArgumentNullException.ThrowIfNull(handler);

        var result = await handler.Handle(
            profileId,
            from ?? DateOnly.FromDateTime(DateTime.UtcNow),
            cancellationToken);

        return result.ToActionResult(this);
    }

    /// <summary>
    /// Cagirici egitmenleri yonetebiliyor mu?
    /// </summary>
    /// <remarks>
    /// Sonuc handler'a bayrak olarak gecer: yetkisi olmayan reddedilmez, yalnizca
    /// kendi profiline yazabilir. Bu yuzden kontrol <c>[Authorize]</c> ile yapilamaz.
    /// </remarks>
    private async Task<bool> CanManageInstructors(IAuthorizationService authorization)
    {
        ArgumentNullException.ThrowIfNull(authorization);

        var result = await authorization.AuthorizeAsync(User, Permissions.Organization.MemberManage);

        return result.Succeeded;
    }
}

/// <summary>Profil kimligi rotadan geldigi icin govdede tasinmaz.</summary>
public sealed record AddInstructorSubjectRequest(Guid SubjectId, Guid? LevelId);

public sealed record AddAvailabilityRequest(
    DayOfWeek DayOfWeek,
    TimeOnly StartLocalTime,
    TimeOnly EndLocalTime,
    DateOnly ValidFrom,
    DateOnly? ValidUntil);

public sealed record AddAvailabilityOverrideRequest(
    DateOnly OverrideDate,
    AvailabilityOverrideType OverrideType,
    TimeOnly? StartLocalTime,
    TimeOnly? EndLocalTime,
    string? Reason);
