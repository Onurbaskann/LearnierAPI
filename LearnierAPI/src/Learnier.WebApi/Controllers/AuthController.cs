using Learnier.Application.Features.Authentication.Commands.LoginUser;
using Learnier.Application.Features.Authentication.Commands.LogoutUser;
using Learnier.Application.Features.Authentication.Commands.RefreshAccessToken;
using Learnier.Application.Features.Authentication.Commands.RequestPasswordReset;
using Learnier.Application.Features.Authentication.Commands.ResetPassword;
using Learnier.Application.Features.Authentication.Commands.RegisterUser;
using Learnier.Application.Features.Authentication.Commands.VerifyEmail;
using Learnier.WebApi.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Learnier.WebApi.Controllers;

/// <summary>
/// Kimlik dogrulama uclari.
/// </summary>
/// <remarks>
/// Bu ucalar organizasyon kapsami disindadir: kullanici henuz hangi kurumda
/// calisacagini secmemistir. Aktif kurum, giris yanitindaki uyelik listesinden
/// secilip sonraki isteklerde <c>X-Organization-Id</c> basligiyla tasinir.
/// </remarks>
[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController : ControllerBase
{
    /// <summary>
    /// Yeni hesap acar.
    /// </summary>
    /// <remarks>
    /// Hesap dogrulanmamis durumda olusur ve dogrulama e-postasi gonderilir;
    /// giris ancak <c>verify-email</c> tamamlandiktan sonra mumkun olur.
    /// </remarks>
    [AllowAnonymous]
    [HttpPost("register")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RegisterUserResult>> Register(
        RegisterUserCommand command,
        [FromServices] RegisterUserHandler handler,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(handler);

        var result = await handler.Handle(command, cancellationToken);

        return result.ToActionResult(this);
    }

    /// <summary>
    /// E-posta dogrulama tokenini tuketir ve hesabi kullanilabilir hale getirir.
    /// </summary>
    [AllowAnonymous]
    [HttpPost("verify-email")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> VerifyEmail(
        VerifyEmailCommand command,
        [FromServices] VerifyEmailHandler handler,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(handler);

        var result = await handler.Handle(command, cancellationToken);

        return result.ToActionResult(this);
    }

    /// <summary>
    /// E-posta ve parola ile giris yapar.
    /// </summary>
    /// <remarks>
    /// Handler action parametresinde <c>[FromServices]</c> ile alinir: bagimlilik
    /// gizlenmez ve derleme zamaninda dogrulanir (bkz. CLAUDE.md, use-case yazimi).
    /// </remarks>
    [AllowAnonymous]
    [HttpPost("login")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<LoginUserResult>> Login(
        LoginUserCommand command,
        [FromServices] LoginUserHandler handler,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(handler);

        var result = await handler.Handle(command, cancellationToken);

        return result.ToActionResult(this);
    }

    /// <summary>
    /// Yenileme tokeni ile yeni bir erisim tokeni alir.
    /// </summary>
    /// <remarks>
    /// Kullanilan yenileme tokeni iptal edilir ve yanitta yenisi doner (rotasyon):
    /// istemci her yenilemede yeni tokeni saklamalidir.
    /// </remarks>
    [AllowAnonymous]
    [HttpPost("refresh")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<RefreshAccessTokenResult>> Refresh(
        RefreshAccessTokenCommand command,
        [FromServices] RefreshAccessTokenHandler handler,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(handler);

        var result = await handler.Handle(command, cancellationToken);

        return result.ToActionResult(this);
    }

    /// <summary>
    /// Mevcut yenileme tokenini iptal ederek oturumu sonlandirir.
    /// </summary>
    [AllowAnonymous]
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> Logout(
        LogoutUserCommand command,
        [FromServices] LogoutUserHandler handler,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(handler);

        var result = await handler.Handle(command, cancellationToken);

        return result.ToActionResult(this);
    }

    /// <summary>
    /// Kayitli hesaba parola sifirlama baglantisi gonderilmesini ister.
    /// </summary>
    [AllowAnonymous]
    [HttpPost("forgot-password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> ForgotPassword(
        RequestPasswordResetCommand command,
        [FromServices] RequestPasswordResetHandler handler,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(handler);

        var result = await handler.Handle(command, cancellationToken);
        return result.ToActionResult(this);
    }

    /// <summary>
    /// Tek kullanimlik tokenla yeni parola belirler.
    /// </summary>
    [AllowAnonymous]
    [HttpPost("reset-password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> ResetPassword(
        ResetPasswordCommand command,
        [FromServices] ResetPasswordHandler handler,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(handler);

        var result = await handler.Handle(command, cancellationToken);
        return result.ToActionResult(this);
    }
}
