using Learnier.Application.Common.Models;
using Learnier.Application.Common.Security;
using Learnier.Application.Features.Catalog.Commands.AddCourseLesson;
using Learnier.Application.Features.Catalog.Commands.AddCourseModule;
using Learnier.Application.Features.Catalog.Commands.ArchiveCourse;
using Learnier.Application.Features.Catalog.Commands.CreateCourse;
using Learnier.Application.Features.Catalog.Commands.PublishCourse;
using Learnier.Application.Features.Catalog.Queries;
using Learnier.Domain.Catalog;
using Learnier.WebApi.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Learnier.WebApi.Controllers;

/// <summary>
/// Egitim tanimlari ve mufredat.
/// </summary>
[ApiController]
[Route("api/v1/courses")]
[Authorize]
public sealed class CoursesController : ControllerBase
{
    /// <summary>Yeni egitim olusturur. Egitim taslak olarak baslar.</summary>
    [HttpPost]
    [Authorize(Policy = Permissions.Course.Manage)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CreateCourseResult>> Create(
        CreateCourseCommand command,
        [FromServices] CreateCourseHandler handler,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(handler);

        var result = await handler.Handle(command, cancellationToken);

        return result.ToActionResult(this);
    }

    /// <summary>Taslak egitimi yayina alir.</summary>
    [HttpPost("{courseId:guid}/publish")]
    [Authorize(Policy = Permissions.Course.Manage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> Publish(
        Guid courseId,
        [FromServices] PublishCourseHandler handler,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(handler);

        var result = await handler.Handle(courseId, cancellationToken);

        return result.ToActionResult(this);
    }

    /// <summary>Egitimi gecmis baglantilarini silmeden arsivler.</summary>
    [HttpPost("{courseId:guid}/archive")]
    [Authorize(Policy = Permissions.Course.Manage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Archive(
        Guid courseId,
        [FromServices] ArchiveCourseHandler handler,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(handler);

        var result = await handler.Handle(courseId, cancellationToken);

        return result.ToActionResult(this);
    }

    /// <summary>
    /// Egitimleri sayfali listeler. Taslaklar yalnizca katalogu yonetenlere gorunur.
    /// </summary>
    [HttpGet]
    [Authorize(Policy = Permissions.Course.Read)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PagedResult<CourseListItem>>> List(
        [FromServices] ListCoursesHandler handler,
        [FromServices] IAuthorizationService authorization,
        CancellationToken cancellationToken,
        [FromQuery] Guid? subjectId = null,
        [FromQuery] Guid? levelId = null,
        [FromQuery] CourseType? courseType = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        ArgumentNullException.ThrowIfNull(handler);

        var result = await handler.Handle(
            new PageRequest { Page = page, PageSize = pageSize },
            subjectId,
            levelId,
            courseType,
            await CanManageCatalog(authorization),
            cancellationToken);

        return result.ToActionResult(this);
    }

    /// <summary>Egitimin mufredatiyla birlikte detayi.</summary>
    [HttpGet("{courseId:guid}")]
    [Authorize(Policy = Permissions.Course.Read)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CourseDetail>> GetDetail(
        Guid courseId,
        [FromServices] GetCourseDetailHandler handler,
        [FromServices] IAuthorizationService authorization,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(handler);

        var result = await handler.Handle(
            courseId,
            await CanManageCatalog(authorization),
            cancellationToken);

        return result.ToActionResult(this);
    }

    /// <summary>Egitime mufredat modulu ekler.</summary>
    [HttpPost("{courseId:guid}/modules")]
    [Authorize(Policy = Permissions.Course.Manage)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AddCourseModuleResult>> AddModule(
        Guid courseId,
        AddCourseModuleRequest request,
        [FromServices] AddCourseModuleHandler handler,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(handler);
        ArgumentNullException.ThrowIfNull(request);

        var result = await handler.Handle(
            new AddCourseModuleCommand(courseId, request.Title, request.SortOrder, request.Description),
            cancellationToken);

        return result.ToActionResult(this);
    }

    /// <summary>Module mufredat dersi ekler.</summary>
    [HttpPost("modules/{moduleId:guid}/lessons")]
    [Authorize(Policy = Permissions.Course.Manage)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AddCourseLessonResult>> AddLesson(
        Guid moduleId,
        AddCourseLessonRequest request,
        [FromServices] AddCourseLessonHandler handler,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(handler);
        ArgumentNullException.ThrowIfNull(request);

        var result = await handler.Handle(
            new AddCourseLessonCommand(
                moduleId,
                request.Title,
                request.SortOrder,
                request.EstimatedDurationMinutes,
                request.Description),
            cancellationToken);

        return result.ToActionResult(this);
    }

    /// <summary>
    /// Cagirici katalogu yonetebiliyor mu?
    /// </summary>
    /// <remarks>
    /// Taslak egitimlerin gorunurlugu buna bagli. Kontrol <c>[Authorize]</c> ile
    /// yapilamaz: yetkisi olmayan reddedilmemeli, yalnizca daha az kayit gormeli.
    /// </remarks>
    private async Task<bool> CanManageCatalog(IAuthorizationService authorization)
    {
        ArgumentNullException.ThrowIfNull(authorization);

        var result = await authorization.AuthorizeAsync(User, Permissions.Course.Manage);

        return result.Succeeded;
    }
}

/// <summary>Egitim kimligi rotadan geldigi icin govdede tasinmaz.</summary>
public sealed record AddCourseModuleRequest(string Title, int SortOrder, string? Description);

/// <summary>Modul kimligi rotadan geldigi icin govdede tasinmaz.</summary>
public sealed record AddCourseLessonRequest(
    string Title,
    int SortOrder,
    int EstimatedDurationMinutes,
    string? Description);
