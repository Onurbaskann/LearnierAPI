using Learnier.Application.Common.Abstractions;
using Learnier.Application.Common.Results;

namespace Learnier.Application.Features.Catalog.Commands.ArchiveCourse;

/// <summary>
/// Egitimi gecmis mufredat ve takvim baglantilarini koruyarak arsivler.
/// </summary>
public sealed class ArchiveCourseHandler(
    ICatalogRepository catalog,
    ICurrentTenant currentTenant,
    IUnitOfWork unitOfWork)
{
    public async Task<Result> Handle(Guid courseId, CancellationToken cancellationToken)
    {
        if (!currentTenant.HasTenant)
        {
            return Result.Failure(CatalogErrors.OrganizationContextRequired);
        }

        var course = await catalog.FindCourseAsync(courseId, includeModules: false, cancellationToken);

        if (course is null)
        {
            return Result.Failure(CatalogErrors.CourseNotFound);
        }

        course.Archive();
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
