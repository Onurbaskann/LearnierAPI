using Learnier.Application.Common.Results;

namespace Learnier.Application.Features.Catalog;

/// <summary>
/// Katalog islemlerinin hata kodlari.
/// </summary>
internal static class CatalogErrors
{
    public static Error OrganizationContextRequired => Error.Forbidden("tenant.organization_required");

    public static Error SubjectSlugAlreadyTaken(string slug)
        => Error.Conflict("catalog.subject_slug_already_taken", ("slug", slug));

    public static Error SubjectNotFound => Error.NotFound("catalog.subject_not_found");

    /// <summary>
    /// Ust alan olarak baska bir kurumun alani veya var olmayan bir alan verildi.
    /// </summary>
    public static Error ParentSubjectNotFound => Error.Validation("catalog.parent_subject_not_found");

    public static Error LevelCodeAlreadyTaken(string code)
        => Error.Conflict("catalog.level_code_already_taken", ("code", code));

    public static Error LevelNotFound => Error.Validation("catalog.level_not_found");

    /// <summary>Seviye, egitimin alanina ait degil.</summary>
    public static Error LevelSubjectMismatch => Error.Validation("catalog.level_subject_mismatch");

    public static Error CourseNotFound => Error.NotFound("catalog.course_not_found");

    public static Error ModuleNotFound => Error.NotFound("catalog.module_not_found");

    /// <summary>Yalnizca taslak durumundaki egitim yayina alinabilir.</summary>
    public static Error CourseNotDraft => Error.Conflict("catalog.course_not_draft");
}
