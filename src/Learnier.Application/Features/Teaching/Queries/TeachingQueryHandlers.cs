using Learnier.Application.Common.Abstractions;
using Learnier.Application.Common.Models;
using Learnier.Application.Common.Results;

namespace Learnier.Application.Features.Teaching.Queries;

/// <summary>
/// Kurumun egitmenlerini sayfali listeler.
/// </summary>
public sealed class ListInstructorsHandler(IInstructorQueries queries, ICurrentTenant currentTenant)
{
    public async Task<Result<PagedResult<InstructorListItem>>> Handle(
        PageRequest page,
        Guid? subjectId,
        CancellationToken cancellationToken)
    {
        if (!currentTenant.HasTenant)
        {
            return TeachingErrors.OrganizationContextRequired;
        }

        var instructors = await queries.ListAsync(page, subjectId, cancellationToken);

        return Result.Success(instructors);
    }
}

/// <summary>
/// Egitmenin yetkinlik ve uygunluklariyla birlikte detayi.
/// </summary>
public sealed class GetInstructorDetailHandler(
    IInstructorQueries queries,
    ICurrentTenant currentTenant)
{
    public async Task<Result<InstructorDetail>> Handle(
        Guid profileId,
        CancellationToken cancellationToken)
    {
        if (!currentTenant.HasTenant)
        {
            return TeachingErrors.OrganizationContextRequired;
        }

        var detail = await queries.FindDetailAsync(profileId, cancellationToken);

        return detail is null
            ? TeachingErrors.ProfileNotFound
            : Result.Success(detail);
    }
}

/// <summary>
/// Egitmenin verilen tarihten itibaren gecerli uygunluk istisnalari.
/// </summary>
public sealed class ListAvailabilityOverridesHandler(
    IInstructorQueries queries,
    ICurrentTenant currentTenant)
{
    public async Task<Result<IReadOnlyList<AvailabilityOverrideDetail>>> Handle(
        Guid profileId,
        DateOnly from,
        CancellationToken cancellationToken)
    {
        if (!currentTenant.HasTenant)
        {
            return TeachingErrors.OrganizationContextRequired;
        }

        var overrides = await queries.ListOverridesAsync(profileId, from, cancellationToken);

        return Result.Success(overrides);
    }
}
