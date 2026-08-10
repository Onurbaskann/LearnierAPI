using Learnier.Application.Common.Results;

namespace Learnier.Application.Features.Teaching;

/// <summary>
/// Egitmen islemlerinin hata kodlari.
/// </summary>
internal static class TeachingErrors
{
    public static Error OrganizationContextRequired => Error.Forbidden("tenant.organization_required");

    public static Error MembershipNotFound => Error.NotFound("teaching.membership_not_found");

    public static Error ProfileAlreadyExists => Error.Conflict("teaching.profile_already_exists");

    public static Error ProfileNotFound => Error.NotFound("teaching.profile_not_found");

    /// <summary>
    /// Cagirici ne bu profilin sahibi ne de egitmenleri yonetme yetkisine sahip.
    /// </summary>
    public static Error ProfileNotOwned => Error.Forbidden("teaching.profile_not_owned");

    public static Error SubjectNotFound => Error.Validation("teaching.subject_not_found");

    public static Error LevelNotFound => Error.Validation("teaching.level_not_found");

    /// <summary>Seviye, yetkinlik icin secilen alana ait degil.</summary>
    public static Error LevelSubjectMismatch => Error.Validation("teaching.level_subject_mismatch");

    /// <summary>
    /// Yeni uygunluk araligi ayni gunde mevcut bir aralikla cakisiyor.
    /// </summary>
    /// <remarks>
    /// Cakisan araliklar slot uretiminde ayni saati iki kez uretir ve egitmen
    /// ayni anda iki oturuma atanabilir hale gelirdi.
    /// </remarks>
    public static Error AvailabilityOverlaps => Error.Conflict("teaching.availability_overlaps");

    public static Error AvailabilityNotFound => Error.NotFound("teaching.availability_not_found");
}
