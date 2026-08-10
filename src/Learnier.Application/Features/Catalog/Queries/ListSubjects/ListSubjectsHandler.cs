using Learnier.Application.Common.Abstractions;
using Learnier.Application.Common.Results;

namespace Learnier.Application.Features.Catalog.Queries.ListSubjects;

/// <summary>
/// Aktif organizasyonun egitim alanlarini listeler.
/// </summary>
/// <remarks>
/// Sayfalama yok: bir kurumun alan sayisi onlarla sinirli kalir ve arayuz genelde
/// tamamini agac olarak gosterir. Egitim listesinde ise sayfalama var, cunku orada
/// kayit sayisi sinirsiz buyuyebilir.
/// </remarks>
public sealed class ListSubjectsHandler(ICatalogQueries queries, ICurrentTenant currentTenant)
{
    public async Task<Result<IReadOnlyList<SubjectListItem>>> Handle(
        bool includeArchived,
        CancellationToken cancellationToken)
    {
        if (!currentTenant.HasTenant)
        {
            return CatalogErrors.OrganizationContextRequired;
        }

        var subjects = await queries.ListSubjectsAsync(includeArchived, cancellationToken);

        return Result.Success(subjects);
    }
}
