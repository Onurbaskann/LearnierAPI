using System.Globalization;
using System.Security.Claims;
using Learnier.Application.Common.Abstractions;
using Microsoft.AspNetCore.Http;

namespace Learnier.WebApi.Common;

/// <summary>
/// Istegi yapan kullaniciyi JWT claim'lerinden okur.
/// </summary>
internal sealed class CurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    public Guid? UserId
    {
        get
        {
            var value = httpContextAccessor.HttpContext?.User
                .FindFirstValue(ClaimTypes.NameIdentifier);

            return Guid.TryParse(value, CultureInfo.InvariantCulture, out var userId)
                ? userId
                : null;
        }
    }

    public bool IsAuthenticated =>
        httpContextAccessor.HttpContext?.User.Identity?.IsAuthenticated ?? false;
}
