using Learnier.Application.Common.Models;
using Learnier.Application.Features.Catalog.Queries;

namespace Learnier.Application.Common.Abstractions;

/// <summary>
/// Katalog okuma sorgulari.
/// </summary>
/// <remarks>
/// <para>
/// Yazma tarafindan (<see cref="ICatalogRepository"/>) ayri: okuma sorgulari
/// varlik yuklemez, dogrudan DTO'ya projekte edilir. Boylece bir listede yalnizca
/// gosterilecek kolonlar okunur ve degisiklik izleme maliyeti odenmez.
/// </para>
/// <para>
/// Tum sorgular kiraci filtresine tabidir; baska kurumun katalogu donmez.
/// </para>
/// </remarks>
public interface ICatalogQueries
{
    Task<IReadOnlyList<SubjectListItem>> ListSubjectsAsync(
        bool includeArchived,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<LevelListItem>> ListLevelsAsync(Guid subjectId, CancellationToken cancellationToken);

    Task<PagedResult<CourseListItem>> ListCoursesAsync(
        CourseListFilter filter,
        CancellationToken cancellationToken);

    /// <summary>
    /// Egitimin mufredatiyla birlikte detayi.
    /// </summary>
    /// <param name="includeUnpublished">
    /// Taslak ve arsivlenmis egitimleri de dondurur. Yalnizca katalogu yonetme
    /// izni olan cagirici icin dogru verilmelidir.
    /// </param>
    Task<CourseDetail?> FindCourseDetailAsync(
        Guid courseId,
        bool includeUnpublished,
        CancellationToken cancellationToken);
}
