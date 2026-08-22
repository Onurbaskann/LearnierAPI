using Learnier.Application.Common.Abstractions;
using Learnier.Application.Common.Results;

namespace Learnier.Application.Features.Catalog.Commands.ArchiveSubject;

/// <summary>
/// Egitim alanini gecmis baglantilarini koruyarak arsivler.
/// </summary>
public sealed class ArchiveSubjectHandler(
    ICatalogRepository catalog,
    IUnitOfWork unitOfWork)
{
    public async Task<Result> Handle(Guid subjectId, CancellationToken cancellationToken)
    {
        var subject = await catalog.FindSubjectAsync(subjectId, cancellationToken);

        if (subject is null)
        {
            return CatalogErrors.SubjectNotFound;
        }

        subject.Archive();
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
