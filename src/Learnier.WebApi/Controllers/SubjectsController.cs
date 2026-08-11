using Learnier.Application.Common.Security;
using Learnier.Application.Features.Catalog.Commands.CreateLevel;
using Learnier.Application.Features.Catalog.Commands.CreateSubject;
using Learnier.Application.Features.Catalog.Commands.ArchiveSubject;
using Learnier.Application.Features.Catalog.Commands.RenameSubject;
using Learnier.Application.Features.Catalog.Queries;
using Learnier.Application.Features.Catalog.Queries.ListSubjects;
using Learnier.WebApi.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Learnier.WebApi.Controllers;

/// <summary>
/// Egitim alanlari ve seviyeleri.
/// </summary>
/// <remarks>
/// Tum uclar aktif organizasyon kapsaminda calisir; kurum
/// <c>X-Organization-Id</c> basligiyla belirtilir.
/// </remarks>
[ApiController]
[Route("api/v1/subjects")]
[Authorize]
public sealed class SubjectsController : ControllerBase
{
    /// <summary>Yeni egitim alani ekler.</summary>
    [HttpPost]
    [Authorize(Policy = Permissions.Course.Manage)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CreateSubjectResult>> Create(
        CreateSubjectCommand command,
        [FromServices] CreateSubjectHandler handler,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(handler);

        var result = await handler.Handle(command, cancellationToken);

        return result.ToActionResult(this);
    }

    /// <summary>Alanlari listeler.</summary>
    /// <param name="includeArchived">Arsivlenmis alanlari da dondurur.</param>
    [HttpGet]
    [Authorize(Policy = Permissions.Course.Read)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<SubjectListItem>>> List(
        [FromServices] ListSubjectsHandler handler,
        CancellationToken cancellationToken,
        [FromQuery] bool includeArchived = false)
    {
        ArgumentNullException.ThrowIfNull(handler);

        var result = await handler.Handle(includeArchived, cancellationToken);

        return result.ToActionResult(this);
    }

    /// <summary>Egitim alaninin gorunen adini degistirir.</summary>
    [HttpPatch("{subjectId:guid}")]
    [Authorize(Policy = Permissions.Course.Manage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Rename(
        Guid subjectId,
        RenameSubjectRequest request,
        [FromServices] RenameSubjectHandler handler,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(handler);

        var result = await handler.Handle(
            new RenameSubjectCommand(subjectId, request.Name),
            cancellationToken);

        return result.ToActionResult(this);
    }

    /// <summary>Egitim alanini gecmis baglantilarini silmeden arsivler.</summary>
    [HttpPost("{subjectId:guid}/archive")]
    [Authorize(Policy = Permissions.Course.Manage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Archive(
        Guid subjectId,
        [FromServices] ArchiveSubjectHandler handler,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(handler);

        var result = await handler.Handle(subjectId, cancellationToken);

        return result.ToActionResult(this);
    }

    /// <summary>Alana seviye ekler.</summary>
    [HttpPost("{subjectId:guid}/levels")]
    [Authorize(Policy = Permissions.Course.Manage)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CreateLevelResult>> CreateLevel(
        Guid subjectId,
        CreateLevelRequest request,
        [FromServices] CreateLevelHandler handler,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(handler);
        ArgumentNullException.ThrowIfNull(request);

        var result = await handler.Handle(
            new CreateLevelCommand(subjectId, request.Code, request.Name, request.SortOrder),
            cancellationToken);

        return result.ToActionResult(this);
    }

    /// <summary>Alanin seviyelerini sirali dondurur.</summary>
    [HttpGet("{subjectId:guid}/levels")]
    [Authorize(Policy = Permissions.Course.Read)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<LevelListItem>>> ListLevels(
        Guid subjectId,
        [FromServices] ListLevelsHandler handler,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(handler);

        var result = await handler.Handle(subjectId, cancellationToken);

        return result.ToActionResult(this);
    }
}

/// <summary>Alan kimligi rotadan geldigi icin govdede tasinmaz.</summary>
public sealed record CreateLevelRequest(string Code, string Name, int SortOrder);

/// <summary>Alan kimligi rotadan geldigi icin govdede tasinmaz.</summary>
public sealed record RenameSubjectRequest(string Name);
