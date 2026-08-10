using Learnier.Application.Common.Security;
using Learnier.Application.Features.Scheduling.Commands.AssignSessionInstructor;
using Learnier.Application.Features.Scheduling.Commands.CancelBooking;
using Learnier.Application.Features.Scheduling.Commands.CreateBooking;
using Learnier.Application.Features.Scheduling.Commands.CreateClassGroup;
using Learnier.Application.Features.Scheduling.Commands.CreateSession;
using Learnier.Application.Features.Scheduling.Commands.EnrollLearner;
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
}

/// <summary>Sinif kimligi rotadan geldigi icin govdede tasinmaz.</summary>
public sealed record EnrollLearnerRequest(Guid LearnerUserId);

public sealed record AssignInstructorRequest(Guid InstructorProfileId, SessionInstructorRole Role);

/// <param name="LearnerUserId">
/// Bos birakilirsa istegi yapan kullanici adina rezervasyon yapilir.
/// </param>
public sealed record CreateBookingRequest(Guid? LearnerUserId);
