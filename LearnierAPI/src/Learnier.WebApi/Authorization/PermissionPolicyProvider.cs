using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace Learnier.WebApi.Authorization;

/// <summary>
/// Izin kodlarini calisma zamaninda policy'ye cevirir.
/// </summary>
/// <remarks>
/// Bu olmadan her izin icin baslangicta tek tek <c>AddPolicy("booking.create", ...)</c>
/// yazmak gerekirdi; yeni bir izin eklendiginde kaydi unutmak sessiz bir guvenlik acigi
/// olustururdu. Burada policy adi dogrudan izin kodu olarak yorumlanir, yani
/// <c>[Authorize(Policy = Permissions.Booking.Create)]</c> ek kayit gerektirmez.
/// </remarks>
internal sealed class PermissionPolicyProvider(IOptions<AuthorizationOptions> options)
    : IAuthorizationPolicyProvider
{
    private readonly DefaultAuthorizationPolicyProvider _fallbackProvider = new(options);

    public Task<AuthorizationPolicy> GetDefaultPolicyAsync()
        => _fallbackProvider.GetDefaultPolicyAsync();

    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync()
        => _fallbackProvider.GetFallbackPolicyAsync();

    public async Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        // Acikca tanimlanmis bir policy varsa o kazanir.
        var existing = await _fallbackProvider.GetPolicyAsync(policyName);
        if (existing is not null)
        {
            return existing;
        }

        // Izin kodlari "alan.eylem" bicimindedir; bu kalibi tasimayan adlar
        // policy olarak yorumlanmaz ve tanimsiz kabul edilir.
        if (!policyName.Contains('.', StringComparison.Ordinal))
        {
            return null;
        }

        return new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .AddRequirements(new PermissionRequirement(policyName))
            .Build();
    }
}
