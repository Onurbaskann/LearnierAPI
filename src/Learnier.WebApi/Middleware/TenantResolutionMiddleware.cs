using System.Globalization;
using Learnier.Application.Common.Abstractions;
using Learnier.WebApi.Common;
using Learnier.WebApi.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace Learnier.WebApi.Middleware;

/// <summary>
/// Istegin uzerinde calisacagi organizasyonu cozer ve uyeligi dogrular.
/// </summary>
/// <remarks>
/// <para>
/// Guvenlik acisindan kritik nokta: organizasyon kimligini <b>istemci</b> gonderir,
/// bu yuzden asla oldugu gibi kabul edilmez. Kullanicinin o organizasyonda aktif
/// uyeligi veritabanindan dogrulanir; dogrulanmazsa istek 403 ile reddedilir.
/// </para>
/// <para>
/// Bu middleware kimlik dogrulamadan <b>sonra</b>, yetkilendirmeden <b>once</b> calismali:
/// izin cozumlemesi uyelige, EF global query filter ise organizasyona bagli.
/// </para>
/// </remarks>
internal sealed class TenantResolutionMiddleware(RequestDelegate next)
{
    /// <summary>
    /// Aktif organizasyonu tasiyan istek basligi.
    /// </summary>
    public const string OrganizationHeaderName = "X-Organization-Id";

    public async Task InvokeAsync(
        HttpContext context,
        CurrentTenant currentTenant,
        ICurrentUser currentUser,
        IMembershipProvider membershipProvider,
        IStringLocalizer<ErrorMessages> localizer)
    {
        ArgumentNullException.ThrowIfNull(context);

        var header = context.Request.Headers[OrganizationHeaderName].ToString();

        // Baslik yoksa istek organizasyon kapsami disindadir (giris, kayit, saglik kontrolu).
        // Bu durumda tenant belirlenmez; organizasyon gerektiren endpoint'ler
        // yetkilendirme asamasinda zaten reddedilir.
        if (string.IsNullOrWhiteSpace(header))
        {
            await next(context);
            return;
        }

        if (!Guid.TryParse(header, CultureInfo.InvariantCulture, out var organizationId))
        {
            await WriteProblem(
                context,
                StatusCodes.Status400BadRequest,
                "tenant.organization_required",
                localizer);
            return;
        }

        if (currentUser.UserId is not { } userId)
        {
            await WriteProblem(
                context,
                StatusCodes.Status401Unauthorized,
                "common.unauthorized",
                localizer);
            return;
        }

        var membership = await membershipProvider.FindActiveMembership(
            userId,
            organizationId,
            context.RequestAborted);

        if (membership is null)
        {
            await WriteProblem(
                context,
                StatusCodes.Status403Forbidden,
                "tenant.membership_not_found",
                localizer);
            return;
        }

        currentTenant.Set(membership.OrganizationId, membership.MembershipId);

        await next(context);
    }

    private static async Task WriteProblem(
        HttpContext context,
        int statusCode,
        string errorCode,
        IStringLocalizer<ErrorMessages> localizer)
    {
        var problem = new ProblemDetails
        {
            Status = statusCode,
            Detail = localizer[errorCode],
            Instance = context.Request.Path
        };

        problem.Extensions["errorCode"] = errorCode;

        context.Response.StatusCode = statusCode;
        await context.Response.WriteAsJsonAsync(problem, context.RequestAborted);
    }
}
