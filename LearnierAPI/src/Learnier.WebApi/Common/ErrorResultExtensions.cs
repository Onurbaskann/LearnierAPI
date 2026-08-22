using Learnier.Application.Common.Results;
using Learnier.WebApi.Localization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Learnier.WebApi.Common;

/// <summary>
/// <see cref="Result"/> tiplerini HTTP yanitina cevirir.
/// </summary>
/// <remarks>
/// HTTP bilgisi yalnizca bu katmanda bulunur: Application katmani
/// <see cref="ErrorType"/> uretir, durum koduna esleme burada yapilir.
/// </remarks>
internal static class ErrorResultExtensions
{
    public static ActionResult ToActionResult(this Result result, ControllerBase controller)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(controller);

        return result.IsSuccess
            ? controller.NoContent()
            : controller.Problem(result.Error);
    }

    public static ActionResult<TValue> ToActionResult<TValue>(
        this Result<TValue> result,
        ControllerBase controller)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(controller);

        return result.IsSuccess
            ? controller.Ok(result.Value)
            : controller.Problem(result.Error);
    }

    private static ObjectResult Problem(this ControllerBase controller, Error error)
    {
        var resolver = controller.HttpContext.RequestServices
            .GetRequiredService<ErrorMessageResolver>();

        var problem = new ProblemDetails
        {
            Status = error.Type.ToStatusCode(),
            Title = error.Type.ToTitle(),
            Detail = resolver.Resolve(error),
            Instance = controller.HttpContext.Request.Path
        };

        // Istemcinin metne degil koda gore dal ayirabilmesi icin kod da yanitta tasinir.
        problem.Extensions["errorCode"] = error.Code;

        return new ObjectResult(problem) { StatusCode = problem.Status };
    }

    public static int ToStatusCode(this ErrorType type) => type switch
    {
        ErrorType.Validation => StatusCodes.Status400BadRequest,
        ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
        ErrorType.Forbidden => StatusCodes.Status403Forbidden,
        ErrorType.NotFound => StatusCodes.Status404NotFound,
        ErrorType.Conflict => StatusCodes.Status409Conflict,
        _ => StatusCodes.Status500InternalServerError
    };

    /// <summary>
    /// ProblemDetails basligi. Bu deger bilerek cevrilmez: RFC 9457'ye gore
    /// title hata turunun sabit tanimidir, kullaniciya gosterilecek metin detail alanidir.
    /// </summary>
    public static string ToTitle(this ErrorType type) => type switch
    {
        ErrorType.Validation => "Validation Failed",
        ErrorType.Unauthorized => "Unauthorized",
        ErrorType.Forbidden => "Forbidden",
        ErrorType.NotFound => "Not Found",
        ErrorType.Conflict => "Conflict",
        _ => "Server Error"
    };
}
