using Learnier.Application.Common.Models;
using Learnier.Application.Common.Security;
using Learnier.Application.Features.Scheduling.Commands.AssignSessionInstructor;
using Learnier.Application.Features.Scheduling.Commands.CancelBooking;
using Learnier.Application.Features.Scheduling.Commands.CancelSession;
using Learnier.Application.Features.Scheduling.Commands.CompleteSession;
using Learnier.Application.Features.Scheduling.Commands.CreateBooking;
using Learnier.Application.Features.Scheduling.Commands.CloseInstructorSlot;
using Learnier.Application.Features.Scheduling.Commands.OpenInstructorSlot;
using Learnier.Application.Features.Scheduling.Commands.CreateClassGroup;
using Learnier.Application.Features.Scheduling.Commands.CreateSession;
using Learnier.Application.Features.Scheduling.Commands.EnrollLearner;
using Learnier.Application.Features.Scheduling.Queries;
using Learnier.Domain.Scheduling;
using Learnier.WebApi.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Learnier.WebApi.Controllers;

/// <summary>
/// Siniflar, oturumlar ve rezervasyonlar.
/// </summary>
[ApiController]
[Route("api/v1")]
[Authorize]
public sealed class SchedulingController : ControllerBase
{
    /// <summary>Siniflari sayfali listeler.</summary>
    [HttpGet("class-groups")]
    [Authorize(Policy = Permissions.Course.Read)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PagedResult<ClassGroupListItem>>> ListClassGroups(
        [FromServices] ListClassGroupsHandler handler,
        CancellationToken cancellationToken,
        [FromQuery] Guid? courseId = null,
        [FromQuery] ClassGroupStatus? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        ArgumentNullException.ThrowIfNull(handler);

        var result = await handler.Handle(
            new PageRequest { Page = page, PageSize = pageSize },
            courseId,
            status,
            cancellationToken);

        return result.ToActionResult(this);
    }

    /// <summary>Sinifin ogrencileriyle birlikte detayi.</summary>
    [HttpGet("class-groups/{classGroupId:guid}")]
    [Authorize(Policy = Permissions.Session.Create)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ClassGroupDetail>> GetClassGroupDetail(
        Guid classGroupId,
        [FromServices] GetClassGroupDetailHandler handler,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(handler);

        var result = await handler.Handle(classGroupId, cancellationToken);

        return result.ToActionResult(this);
    }

    /// <summary>Egitim icin sinif olusturur.</summary>
    [HttpPost("class-groups")]
    [Authorize(Policy = Permissions.Session.Create)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CreateClassGroupResult>> CreateClassGroup(
        CreateClassGroupCommand command,
        [FromServices] CreateClassGroupHandler handler,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(handler);

        var result = await handler.Handle(command, cancellationToken);

        return result.ToActionResult(this);
    }

    /// <summary>Ogrenciyi sinifa kaydeder.</summary>
    [HttpPost("class-groups/{classGroupId:guid}/members")]
    [Authorize(Policy = Permissions.Session.Create)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<EnrollLearnerResult>> EnrollLearner(
        Guid classGroupId,
        EnrollLearnerRequest request,
        [FromServices] EnrollLearnerHandler handler,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(handler);
        ArgumentNullException.ThrowIfNull(request);

        var result = await handler.Handle(
            new EnrollLearnerCommand(classGroupId, request.LearnerUserId),
            cancellationToken);

        return result.ToActionResult(this);
    }

    /// <summary>Takvime oturum ekler.</summary>
    [HttpPost("sessions")]
    [Authorize(Policy = Permissions.Session.Create)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CreateSessionResult>> CreateSession(
        CreateSessionCommand command,
        [FromServices] CreateSessionHandler handler,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(handler);

        var result = await handler.Handle(command, cancellationToken);

        return result.ToActionResult(this);
    }

    /// <summary>Takvim oturumlarini sayfali listeler.</summary>
    [HttpGet("sessions")]
    [Authorize(Policy = Permissions.Course.Read)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PagedResult<SessionListItem>>> ListSessions(
        [FromServices] ListSessionsHandler handler,
        CancellationToken cancellationToken,
        [FromQuery] Guid? courseId = null,
        [FromQuery] DateTimeOffset? from = null,
        [FromQuery] DateTimeOffset? to = null,
        [FromQuery] LessonSessionStatus? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        ArgumentNullException.ThrowIfNull(handler);

        var result = await handler.Handle(
            new PageRequest { Page = page, PageSize = pageSize },
            courseId,
            from,
            to,
            status,
            cancellationToken);

        return result.ToActionResult(this);
    }

    /// <summary>Oturumun egitmenleri ve kontenjan durumuyla detayi.</summary>
    [HttpGet("sessions/{sessionId:guid}")]
    [Authorize(Policy = Permissions.Course.Read)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SessionDetail>> GetSessionDetail(
        Guid sessionId,
        [FromServices] GetSessionDetailHandler handler,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(handler);

        var result = await handler.Handle(sessionId, cancellationToken);

        return result.ToActionResult(this);
    }

    /// <summary>Oturumu ve tum aktif rezervasyonlarini iptal eder.</summary>
    [HttpPost("sessions/{sessionId:guid}/cancel")]
    [Authorize(Policy = Permissions.Session.Cancel)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CancelSessionResult>> CancelSession(
        Guid sessionId,
        CancelSessionRequest request,
        [FromServices] CancelSessionHandler handler,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(handler);

        var result = await handler.Handle(
            new CancelSessionCommand(sessionId, request.Reason),
            cancellationToken);

        return result.ToActionResult(this);
    }

    /// <summary>Dersi katilim sonuclariyla tamamlar ve ayrilan kredileri tuketir.</summary>
    [HttpPost("sessions/{sessionId:guid}/complete")]
    [Authorize(Policy = Permissions.Session.Complete)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CompleteSessionResult>> CompleteSession(
        Guid sessionId,
        CompleteSessionRequest request,
        [FromServices] CompleteSessionHandler handler,
        [FromServices] IAuthorizationService authorization,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(handler);

        var command = new CompleteSessionCommand(
            sessionId,
            request.Attendances.Select(item => new CompleteSessionAttendance(
                item.BookingId,
                item.Status,
                item.AttendedMinutes,
                item.JoinedAt,
                item.LeftAt)).ToList());

        var result = await handler.Handle(
            command,
            await CanManageCourses(authorization),
            cancellationToken);

        return result.ToActionResult(this);
    }

    /// <summary>Oturuma egitmen atar.</summary>
    /// <remarks>
    /// Egitmen ayni saatte baska bir oturuma atanmissa istek reddedilir.
    /// </remarks>
    [HttpPost("sessions/{sessionId:guid}/instructors")]
    [Authorize(Policy = Permissions.Session.Create)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> AssignInstructor(
        Guid sessionId,
        AssignInstructorRequest request,
        [FromServices] AssignSessionInstructorHandler handler,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(handler);
        ArgumentNullException.ThrowIfNull(request);

        var result = await handler.Handle(
            new AssignSessionInstructorCommand(sessionId, request.InstructorProfileId, request.Role),
            cancellationToken);

        return result.ToActionResult(this);
    }

    /// <summary>
    /// Oturuma rezervasyon yapar. Kontenjan doluysa bekleme listesine alinir.
    /// </summary>
    [HttpPost("sessions/{sessionId:guid}/bookings")]
    [Authorize(Policy = Permissions.Booking.Create)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CreateBookingResult>> CreateBooking(
        Guid sessionId,
        CreateBookingRequest? request,
        [FromServices] CreateBookingHandler handler,
        [FromServices] IAuthorizationService authorization,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(handler);

        var result = await handler.Handle(
            new CreateBookingCommand(sessionId, request?.LearnerUserId),
            await CanManageAllBookings(authorization),
            cancellationToken);

        return result.ToActionResult(this);
    }

    /// <summary>Egitmen kendi takviminde tek bir birebir ders slotu acar.</summary>
    [HttpPost("instructors/me/slots")]
    [Authorize(Policy = Permissions.Session.Create)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<OpenInstructorSlotResult>> OpenMyInstructorSlot(
        OpenInstructorSlotCommand command,
        [FromServices] OpenInstructorSlotHandler handler,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(handler);

        var result = await handler.Handle(command, cancellationToken);

        return result.ToActionResult(this);
    }

    /// <summary>Egitmen kendisine ait bos bir slotu kapatir.</summary>
    [HttpDelete("instructors/me/slots/{sessionId:guid}")]
    [Authorize(Policy = Permissions.Session.Create)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> CloseMyInstructorSlot(
        Guid sessionId,
        [FromServices] CloseInstructorSlotHandler handler,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(handler);

        var result = await handler.Handle(
            new CloseInstructorSlotCommand(sessionId),
            cancellationToken);

        return result.ToActionResult(this);
    }

    /// <summary>Egitmenin ders ve tarih araligindaki somut rezervasyon slotlarini listeler.</summary>
    [HttpGet("instructors/{instructorProfileId:guid}/slots")]
    [Authorize(Policy = Permissions.Booking.Create)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<InstructorSlotListItem>>> ListInstructorSlots(
        Guid instructorProfileId,
        Guid? courseId,
        DateTimeOffset from,
        DateTimeOffset until,
        [FromServices] ListInstructorSlotsHandler handler,
        CancellationToken cancellationToken,
        [FromQuery] int? lessonDurationMinutes = null)
    {
        ArgumentNullException.ThrowIfNull(handler);

        var result = await handler.Handle(
            new ListInstructorSlotsQuery(
                instructorProfileId,
                courseId,
                from,
                until,
                lessonDurationMinutes),
            cancellationToken);

        return result.ToActionResult(this);
    }

    /// <summary>Egitmenin actigi gelecek slotlari kendi paneli icin listeler.</summary>
    [HttpGet("instructors/me/slots")]
    [Authorize(Policy = Permissions.Session.Create)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<InstructorSlotListItem>>> ListMyInstructorSlots(
        DateTimeOffset from,
        DateTimeOffset until,
        [FromServices] ListMyInstructorSlotsHandler handler,
        CancellationToken cancellationToken,
        [FromQuery] Guid? courseId = null)
    {
        var result = await handler.Handle(
            courseId,
            from,
            until,
            cancellationToken);

        return result.ToActionResult(this);
    }

    /// <summary>Oturum acan kullanicinin kendi rezervasyonlarini listeler.</summary>
    [HttpGet("bookings/me")]
    [Authorize(Policy = Permissions.Booking.Create)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PagedResult<LearnerBookingListItem>>> ListMyBookings(
        [FromServices] ListMyBookingsHandler handler,
        CancellationToken cancellationToken,
        [FromQuery] DateTimeOffset? from = null,
        [FromQuery] DateTimeOffset? to = null,
        [FromQuery] BookingStatus? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        ArgumentNullException.ThrowIfNull(handler);

        var result = await handler.Handle(
            new PageRequest { Page = page, PageSize = pageSize },
            from,
            to,
            status,
            cancellationToken);

        return result.ToActionResult(this);
    }

    /// <summary>
    /// Rezervasyonu iptal eder. Yer bosalirsa bekleme listesindeki ilk kayit yukseltilir.
    /// </summary>
    [HttpDelete("bookings/{bookingId:guid}")]
    [Authorize(Policy = Permissions.Booking.Create)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CancelBookingResult>> CancelBooking(
        Guid bookingId,
        [FromServices] CancelBookingHandler handler,
        [FromServices] IAuthorizationService authorization,
        CancellationToken cancellationToken,
        [FromQuery] string? reason = null)
    {
        ArgumentNullException.ThrowIfNull(handler);

        var result = await handler.Handle(
            new CancelBookingCommand(bookingId, reason),
            await CanManageAllBookings(authorization),
            cancellationToken);

        return result.ToActionResult(this);
    }

    /// <summary>
    /// Cagirici baskasi adina rezervasyon yonetebiliyor mu?
    /// </summary>
    /// <remarks>
    /// Yetkisi olmayan reddedilmez, yalnizca kendi adina islem yapabilir; bu yuzden
    /// kontrol <c>[Authorize]</c> ile degil bayrakla yapiliyor.
    /// </remarks>
    private async Task<bool> CanManageAllBookings(IAuthorizationService authorization)
    {
        ArgumentNullException.ThrowIfNull(authorization);

        var result = await authorization.AuthorizeAsync(User, Permissions.Booking.ManageAll);

        return result.Succeeded;
    }

    private async Task<bool> CanManageCourses(IAuthorizationService authorization)
    {
        ArgumentNullException.ThrowIfNull(authorization);

        var result = await authorization.AuthorizeAsync(User, Permissions.Course.Manage);

        return result.Succeeded;
    }
}

/// <summary>Sinif kimligi rotadan geldigi icin govdede tasinmaz.</summary>
public sealed record EnrollLearnerRequest(Guid LearnerUserId);

public sealed record AssignInstructorRequest(Guid InstructorProfileId, SessionInstructorRole Role);

public sealed record CancelSessionRequest(string? Reason);

public sealed record CompleteSessionAttendanceRequest(
    Guid BookingId,
    AttendanceStatus Status,
    int AttendedMinutes,
    DateTimeOffset? JoinedAt = null,
    DateTimeOffset? LeftAt = null);

public sealed record CompleteSessionRequest(
    IReadOnlyList<CompleteSessionAttendanceRequest> Attendances);

/// <param name="LearnerUserId">
/// Bos birakilirsa istegi yapan kullanici adina rezervasyon yapilir.
/// </param>
public sealed record CreateBookingRequest(Guid? LearnerUserId);
