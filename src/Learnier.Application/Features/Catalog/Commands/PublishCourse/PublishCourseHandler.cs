using Learnier.Application.Common.Abstractions;
using Learnier.Application.Common.Results;
using Learnier.Domain.Catalog;

namespace Learnier.Application.Features.Catalog.Commands.PublishCourse;

/// <summary>
/// Taslak egitimi yayina alir.
/// </summary>
/// <remarks>
/// Yayinlanan egitim katalog listelerinde gorunur hale gelir; taslak olanlar
/// yalnizca katalogu yonetenlere gosterilir.
/// </remarks>
public sealed class PublishCourseHandler(
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

        // Arsivlenmis bir egitim sessizce yayina donmemeli; durum acikca bildirilir.
        if (course.Status is not CourseStatus.Draft)
        {
            return Result.Failure(CatalogErrors.CourseNotDraft);
        }

        course.Publish();
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
