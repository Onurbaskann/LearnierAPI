using Learnier.Domain.Catalog;

namespace Learnier.Application.Common.Abstractions;

/// <summary>
/// Katalog yazma islemleri.
/// </summary>
/// <remarks>
/// Okuma tarafi burada degil: listeleme ve detay sorgulari dogrudan DTO'ya
/// projekte edildigi icin <see cref="ICatalogQueries"/> uzerinden yapilir.
/// Ayirmak, depo arayuzunun her ekran icin yeni metotla sismesini engeller.
/// </remarks>
public interface ICatalogRepository
{
    Task<bool> SubjectSlugExistsAsync(Guid organizationId, string slug, CancellationToken cancellationToken);

    /// <summary>
    /// Alani bulur. Kiraci filtresi geregi baska kurumun alani donmez.
    /// </summary>
    Task<Subject?> FindSubjectAsync(Guid subjectId, CancellationToken cancellationToken);

    Task<bool> LevelCodeExistsAsync(Guid subjectId, string code, CancellationToken cancellationToken);

    Task<Level?> FindLevelAsync(Guid levelId, CancellationToken cancellationToken);

    /// <summary>
    /// Egitimi bulur. <paramref name="includeModules"/> mufredat duzenlerken gerekir.
    /// </summary>
    Task<Course?> FindCourseAsync(Guid courseId, bool includeModules, CancellationToken cancellationToken);

    Task<CourseModule?> FindModuleWithCourseAsync(Guid moduleId, CancellationToken cancellationToken);

    void AddSubject(Subject subject);

    void AddLevel(Level level);

    void AddCourse(Course course);
}
