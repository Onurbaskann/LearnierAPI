using Learnier.Application.Features.Authentication.Commands.LoginUser;
using Learnier.Application.Features.Authentication.Commands.RefreshAccessToken;
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
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
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
}
