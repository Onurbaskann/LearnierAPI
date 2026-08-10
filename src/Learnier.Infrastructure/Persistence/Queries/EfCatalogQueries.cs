using Learnier.Application.Common.Abstractions;
using Learnier.Application.Common.Models;
using Learnier.Application.Features.Catalog.Queries;
using Learnier.Domain.Catalog;
using Microsoft.EntityFrameworkCore;

namespace Learnier.Infrastructure.Persistence.Queries;

/// <inheritdoc cref="ICatalogQueries"/>
/// <remarks>
/// Tum sorgular <c>AsNoTracking</c> ve dogrudan DTO projeksiyonu kullanir:
/// okunan kolonlar yalnizca yanitta gorunenlerdir.
/// </remarks>
internal sealed class EfCatalogQueries(AppDbContext context) : ICatalogQueries
{
    public async Task<IReadOnlyList<SubjectListItem>> ListSubjectsAsync(
        bool includeArchived,
        CancellationToken cancellationToken)
    {
        var query = context.Subjects.AsNoTracking();

        if (!includeArchived)
        {
            query = query.Where(s => s.Status == SubjectStatus.Active);
        }

        return await query
            .OrderBy(s => s.Name)
            .Select(s => new SubjectListItem(
                s.Id,
                s.Name,
                s.Slug,
                s.ParentSubjectId,
                s.Status,
                // Alt sorgu: alan basina egitim sayisi. Ayri bir istekle
                // hesaplanmasi N+1 olurdu.
                context.Courses.Count(c => c.SubjectId == s.Id)))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<LevelListItem>> ListLevelsAsync(
        Guid subjectId,
        CancellationToken cancellationToken)
        => await context.Levels
            .AsNoTracking()
            // Level kiraci filtresi tasimaz; sinir Subject uzerinden korunur.
            .Where(l => l.SubjectId == subjectId)
            .Where(l => context.Subjects.Any(s => s.Id == l.SubjectId))
            .OrderBy(l => l.SortOrder)
            .Select(l => new LevelListItem(l.Id, l.Code, l.Name, l.SortOrder))
            .ToListAsync(cancellationToken);

    public async Task<PagedResult<CourseListItem>> ListCoursesAsync(
        CourseListFilter filter,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var query = context.Courses.AsNoTracking();

        if (!filter.IncludeUnpublished)
        {
            query = query.Where(c => c.Status == CourseStatus.Published);
        }

        if (filter.SubjectId is { } subjectId)
        {
            query = query.Where(c => c.SubjectId == subjectId);
        }

        if (filter.LevelId is { } levelId)
        {
            query = query.Where(c => c.LevelId == levelId);
        }

        if (filter.CourseType is { } courseType)
        {
            query = query.Where(c => c.CourseType == courseType);
        }

        // Sayfa disi toplam: istemcinin sayfa sayisini hesaplayabilmesi icin.
        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            // Ikincil siralama anahtari zorunlu: yalnizca Title ile siralanirsa
            // ayni baslikli kayitlarin sirasi sayfalar arasinda degisebilir ve
            // bir kayit iki sayfada birden gorunebilir.
            .OrderBy(c => c.Title)
            .ThenBy(c => c.Id)
            .Skip(filter.Page.Skip)
            .Take(filter.Page.PageSize)
            .Select(c => new CourseListItem(
                c.Id,
                c.Title,
                c.SubjectId,
                c.Subject.Name,
                context.Levels
                    .Where(l => l.Id == c.LevelId)
                    .Select(l => l.Code)
                    .FirstOrDefault(),
                c.CourseType,
                c.Status,
                c.DefaultDurationMinutes,
                c.MaxParticipants))
            .ToListAsync(cancellationToken);

        return new PagedResult<CourseListItem>(
            items,
            filter.Page.Page,
            filter.Page.PageSize,
            totalCount);
    }

    public async Task<CourseDetail?> FindCourseDetailAsync(
        Guid courseId,
        bool includeUnpublished,
        CancellationToken cancellationToken)
    {
        var query = context.Courses.AsNoTracking().Where(c => c.Id == courseId);

        if (!includeUnpublished)
        {
            query = query.Where(c => c.Status == CourseStatus.Published);
        }

        return await query
            .Select(c => new CourseDetail(
                c.Id,
                c.Title,
                c.Description,
                c.SubjectId,
                c.Subject.Name,
                context.Levels
                    .Where(l => l.Id == c.LevelId)
                    .Select(l => l.Code)
                    .FirstOrDefault(),
                c.CourseType,
                c.Status,
                c.DefaultDurationMinutes,
                c.MinParticipants,
                c.MaxParticipants,
                c.Modules
                    .OrderBy(m => m.SortOrder)
                    .Select(m => new CourseModuleDetail(
                        m.Id,
                        m.Title,
                        m.Description,
                        m.SortOrder,
                        m.Lessons
                            .OrderBy(l => l.SortOrder)
                            .Select(l => new CourseLessonDetail(
                                l.Id,
                                l.Title,
                                l.Description,
                                l.SortOrder,
                                l.EstimatedDurationMinutes))
                            .ToList()))
                    .ToList()))
            .FirstOrDefaultAsync(cancellationToken);
    }
}
