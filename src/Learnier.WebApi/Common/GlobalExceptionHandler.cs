using Learnier.WebApi.Localization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace Learnier.WebApi.Common;

/// <summary>
/// Yakalanmamis istisnalari RFC 9457 uyumlu ProblemDetails yanitina cevirir.
/// </summary>
/// <remarks>
/// Buraya yalnizca <b>beklenmeyen</b> hatalar duser. Is kurali ihlalleri
/// (kontenjan dolu, iptal suresi gecti gibi) istisna degil <c>Result</c> ile tasinir.
/// Bu ayrimin pratik faydasi: buradaki her kayit gercekten incelenmesi gereken bir olaydir.
/// </remarks>
internal sealed partial class GlobalExceptionHandler(
    ILogger<GlobalExceptionHandler> logger,
    IStringLocalizer<ErrorMessages> localizer)
    : IExceptionHandler
{
    // Kaynak uretecli log tanimi: her cagrida dizi ayirmaz ve seviye kapaliysa
    // parametreleri hic bicimlendirmez.
    // Log mesajlari gelistirici icindir: Ingilizce ve lokalize edilmez.
    [LoggerMessage(
        EventId = 1000,
        Level = LogLevel.Error,
        Message = "Unhandled exception for {Method} {Path}")]
    private static partial void LogUnhandledException(
        ILogger logger,
        Exception exception,
        string method,
        string path);

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        LogUnhandledException(
            logger,
            exception,
            httpContext.Request.Method,
            httpContext.Request.Path);

        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "Server Error",
            // Istisna detaylari istemciye sizdirilmaz; ayrinti loglarda kalir.
            Detail = localizer["common.unexpected_error"],
            Instance = httpContext.Request.Path
        };

        problem.Extensions["errorCode"] = "common.unexpected_error";
        problem.Extensions["traceId"] = httpContext.TraceIdentifier;

        httpContext.Response.StatusCode = problem.Status.Value;
        await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);

        return true;
    }
}
