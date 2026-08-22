using Learnier.Application.Common.Results;

namespace Learnier.Application.Features.Organizations;

/// <summary>
/// Organizasyon ve uyelik islemlerinin hata kodlari.
/// </summary>
internal static class OrganizationErrors
{
    public static Error SlugAlreadyTaken(string slug)
        => Error.Conflict("organization.slug_already_taken", ("slug", slug));

    public static Error NotFound => Error.NotFound("organization.not_found");

    public static Error UserAlreadyMember => Error.Conflict("organization.user_already_member");

    public static Error MembershipNotFound => Error.NotFound("organization.membership_not_found");

    /// <summary>
    /// Davet edilmek istenen e-posta ile kayitli kullanici yok.
    /// </summary>
    /// <remarks>
    /// Kayitli olmayan kisiye davet gonderme akisi kurulmadi; kaynak dokumanin
    /// 14. bolumu geregi ayri bir davet/token yapisi ilk surume alinmiyor.
    /// </remarks>
    public static Error UserNotFound => Error.NotFound("organization.user_not_found");

    /// <summary>Rol baska bir kuruma ait veya hic yok.</summary>
    public static Error RoleNotUsable => Error.Validation("organization.role_not_usable");

    /// <summary>Istek organizasyon baglami gerektiriyor ama X-Organization-Id gelmemis.</summary>
    public static Error OrganizationContextRequired => Error.Forbidden("tenant.organization_required");
}
