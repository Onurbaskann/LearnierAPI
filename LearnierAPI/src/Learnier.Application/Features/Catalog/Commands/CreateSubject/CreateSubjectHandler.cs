using Learnier.Application.Common.Abstractions;
using Learnier.Application.Common.Results;
using Learnier.Domain.Catalog;

namespace Learnier.Application.Features.Catalog.Commands.CreateSubject;

/// <summary>
/// Aktif organizasyona yeni egitim alani ekler.
/// </summary>
public sealed class CreateSubjectHandler(
    ICatalogRepository catalog,
    ICurrentTenant currentTenant,
    IUnitOfWork unitOfWork)
{
    public async Task<Result<CreateSubjectResult>> Handle(
        CreateSubjectCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (currentTenant.OrganizationId is not { } organizationId)
        {
            return CatalogErrors.OrganizationContextRequired;
        }

        var slug = command.Slug.Trim().ToLowerInvariant();

        if (await catalog.SubjectSlugExistsAsync(organizationId, slug, cancellationToken))
        {
            return CatalogErrors.SubjectSlugAlreadyTaken(slug);
        }

        if (command.ParentSubjectId is { } parentId)
        {
            // Sorgu kiraci filtresine tabi: baska kurumun alani ust alan olarak
            // secilemez, kimligi bilinse bile bulunamaz.
            var parent = await catalog.FindSubjectAsync(parentId, cancellationToken);

            if (parent is null)
            {
                return CatalogErrors.ParentSubjectNotFound;
            }
        }

        var subject = Subject.Create(organizationId, command.Name, slug, command.ParentSubjectId);

        catalog.AddSubject(subject);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new CreateSubjectResult(subject.Id, subject.Slug);
    }
}
