using Learnier.Application.Common.Abstractions;
using Learnier.Application.Common.Results;

namespace Learnier.Application.Features.Catalog.Commands.RenameSubject;

/// <summary>
/// Egitim alaninin gorunen adini degistirir; URL sabitligi icin slug korunur.
/// </summary>
public sealed class RenameSubjectHandler(
    ICatalogRepository catalog,
    IUnitOfWork unitOfWork)
{
    public async Task<Result> Handle(
        Guid subjectId,
        RenameSubjectCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var subject = await catalog.FindSubjectAsync(subjectId, cancellationToken);

        if (subject is null)
        {
            return CatalogErrors.SubjectNotFound;
        }

        subject.Rename(command.Name);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
