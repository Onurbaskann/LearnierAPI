using Learnier.Application.Common.Abstractions;
using Learnier.Domain.Catalog;
using Microsoft.EntityFrameworkCore;

namespace Learnier.Infrastructure.Persistence.Repositories;

/// <inheritdoc cref="ICatalogRepository"/>
internal sealed class EfCatalogRepository(AppDbContext context) : ICatalogRepository
{
    public async Task<bool> SubjectSlugExistsAsync(
        Guid organizationId,
        string slug,
        CancellationToken cancellationToken)
        => await context.Subjects.AnyAsync(
            s => s.OrganizationId == organizationId && s.Slug == slug,
            cancellationToken);

    public async Task<Subject?> FindSubjectAsync(Guid subjectId, CancellationToken cancellationToken)
        => await context.Subjects.FirstOrDefaultAsync(s => s.Id == subjectId, cancellationToken);

    public async Task<bool> LevelCodeExistsAsync(
        Guid subjectId,
        string code,
        CancellationToken cancellationToken)
        => await context.Levels.AnyAsync(
            l => l.SubjectId == subjectId && l.Code == code,
            cancellationToken);

    // Level kendi OrganizationId'sini tasimaz; kiraci sinirini Subject uzerinden
    // acikca dogruluyoruz, aksi halde baska kurumun seviyesi bulunabilirdi.
    public async Task<Level?> FindLevelAsync(Guid levelId, CancellationToken cancellationToken)
        => await context.Levels
            .Where(l => l.Id == levelId)
            .Where(l => context.Subjects.Any(s => s.Id == l.SubjectId))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<Course?> FindCourseAsync(
        Guid courseId,
        bool includeModules,
        CancellationToken cancellationToken)
    {
        var query = context.Courses.AsQueryable();

        if (includeModules)
        {
            query = query.Include(c => c.Modules);
        }

        return await query.FirstOrDefaultAsync(c => c.Id == courseId, cancellationToken);
    }

    // Modul kendi OrganizationId'sini tasimaz; erisim Course uzerinden dogrulanir.
    // Course kiraci filtresine tabi oldugu icin bu join siniri korur.
    public async Task<CourseModule?> FindModuleWithCourseAsync(
        Guid moduleId,
        CancellationToken cancellationToken)
        => await context.CourseModules
            .Include(m => m.Course)
            .Where(m => m.Id == moduleId)
            .Where(m => context.Courses.Any(c => c.Id == m.CourseId))
            .FirstOrDefaultAsync(cancellationToken);

    public void AddSubject(Subject subject) => context.Subjects.Add(subject);

    public void AddLevel(Level level) => context.Levels.Add(level);

    public void AddCourse(Course course) => context.Courses.Add(course);
}
