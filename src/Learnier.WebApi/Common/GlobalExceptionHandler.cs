using Learnier.WebApi.Localization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Npgsql;

namespace Learnier.WebApi.Common;

/// <summary>
/// Yakalanmamis istisnalari RFC 9457 uyumlu ProblemDetails yanitina cevirir.
/// </summary>
/// <remarks>
/// Is kurali ihlalleri (kontenjan dolu, iptal suresi gecti gibi) istisna degil
/// <c>Result</c> ile tasinir. Yalnizca veritabaninda kesinlesebilen yaris kosullari
/// burada bilinen istemci hatalarina donusturulur; diger kayitlar incelenmesi gereken
/// beklenmeyen olaylardir.
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

        var isFriendshipPairConflict = exception is DbUpdateException
        {
            InnerException: PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation,
                ConstraintName: "ix_friendships_first_user_id_second_user_id"
            }
        };
        var status = isFriendshipPairConflict
            ? StatusCodes.Status409Conflict
            : StatusCodes.Status500InternalServerError;
        var errorCode = isFriendshipPairConflict
            ? "friends.request_already_pending"
            : "common.unexpected_error";

        var problem = new ProblemDetails
        {
            Status = status,
            Title = isFriendshipPairConflict ? "Conflict" : "Server Error",
            // Istisna detaylari istemciye sizdirilmaz; ayrinti loglarda kalir.
            Detail = localizer[errorCode],
            Instance = httpContext.Request.Path
        };

        problem.Extensions["errorCode"] = errorCode;
        problem.Extensions["traceId"] = httpContext.TraceIdentifier;

        httpContext.Response.StatusCode = problem.Status.Value;
        await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);

        return true;
    }
}
