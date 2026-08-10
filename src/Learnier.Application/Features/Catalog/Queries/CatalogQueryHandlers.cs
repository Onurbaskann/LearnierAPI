using Learnier.Application.Common.Abstractions;
using Learnier.Application.Common.Models;
using Learnier.Application.Common.Results;

namespace Learnier.Application.Features.Catalog.Queries;

/// <summary>
/// Bir alanin seviyelerini sirali dondurur.
/// </summary>
public sealed class ListLevelsHandler(ICatalogQueries queries, ICurrentTenant currentTenant)
{
    public async Task<Result<IReadOnlyList<LevelListItem>>> Handle(
        Guid subjectId,
        CancellationToken cancellationToken)
    {
        if (!currentTenant.HasTenant)
        {
            return CatalogErrors.OrganizationContextRequired;
        }

        var levels = await queries.ListLevelsAsync(subjectId, cancellationToken);

        return Result.Success(levels);
    }
}

/// <summary>
/// Egitimleri sayfali listeler.
/// </summary>
/// <remarks>
/// <paramref name="canManageCatalog"/> caller tarafindan verilir ve taslak
/// egitimlerin gorunurlugunu belirler. Yetkiyi handler'in kendisi cozmez:
/// izin kontrolu WebApi katmaninin isi, burada yalnizca sonucu kullanilir.
/// </remarks>
public sealed class ListCoursesHandler(ICatalogQueries queries, ICurrentTenant currentTenant)
{
    public async Task<Result<PagedResult<CourseListItem>>> Handle(
        PageRequest page,
        Guid? subjectId,
        Guid? levelId,
        Domain.Catalog.CourseType? courseType,
        bool canManageCatalog,
        CancellationToken cancellationToken)
    {
        if (!currentTenant.HasTenant)
        {
            return CatalogErrors.OrganizationContextRequired;
        }

        var filter = new CourseListFilter(page, subjectId, levelId, courseType, canManageCatalog);
        var courses = await queries.ListCoursesAsync(filter, cancellationToken);

        return Result.Success(courses);
    }
}

/// <summary>
/// Egitimin mufredatiyla birlikte detayini dondurur.
/// </summary>
public sealed class GetCourseDetailHandler(ICatalogQueries queries, ICurrentTenant currentTenant)
{
    public async Task<Result<CourseDetail>> Handle(
        Guid courseId,
        bool canManageCatalog,
        CancellationToken cancellationToken)
    {
        if (!currentTenant.HasTenant)
        {
            return CatalogErrors.OrganizationContextRequired;
        }

        var detail = await queries.FindCourseDetailAsync(courseId, canManageCatalog, cancellationToken);

        // Yayinlanmamis egitim, yetkisi olmayan icin "bulunamadi" doner:
        // "var ama goremezsin" demek katalogun varligini ele verirdi.
        return detail is null
            ? CatalogErrors.CourseNotFound
            : Result.Success(detail);
    }
}
