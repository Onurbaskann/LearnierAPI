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

public sealed class GetMyInstructorDashboardHandler(
    IInstructorQueries queries,
    ICurrentTenant currentTenant,
    IClock clock)
{
    public async Task<Result<InstructorDashboardStats>> Handle(
        CancellationToken cancellationToken)
    {
        if (currentTenant.MembershipId is not { } membershipId)
        {
            return TeachingErrors.OrganizationContextRequired;
        }

        var now = clock.UtcNow;
        var monthStartsAt = new DateTimeOffset(
            now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);
        var monthEndsAt = monthStartsAt.AddMonths(1);
        var result = await queries.FindMyDashboardAsync(
            membershipId, monthStartsAt, monthEndsAt, cancellationToken);

        return result is null ? TeachingErrors.ProfileNotFound : Result.Success(result);
    }
}

public sealed class ListMyInstructorStudentsHandler(
    IInstructorQueries queries,
    ICurrentTenant currentTenant)
{
    public async Task<Result<IReadOnlyList<InstructorStudentListItem>>> Handle(
        CancellationToken cancellationToken)
    {
        if (currentTenant.MembershipId is not { } membershipId)
        {
            return TeachingErrors.OrganizationContextRequired;
        }

        var result = await queries.ListMyStudentsAsync(membershipId, cancellationToken);
        return result is null ? TeachingErrors.ProfileNotFound : Result.Success(result);
    }
}

public sealed class ListMyInstructorScheduleHandler(
    IInstructorQueries queries,
    IInstructorRepository instructors,
    IInstructorCompensationService compensation,
    ICurrentTenant currentTenant,
    IClock clock)
{
    public async Task<Result<IReadOnlyList<InstructorScheduleListItem>>> Handle(
        DateTimeOffset? from,
        DateTimeOffset? until,
        CancellationToken cancellationToken)
    {
        if (currentTenant.MembershipId is not { } membershipId)
        {
            return TeachingErrors.OrganizationContextRequired;
        }

        var result = await queries.ListMyScheduleAsync(
            membershipId,
            from,
            until,
            clock.UtcNow,
            cancellationToken);
        if (result is null)
        {
            return TeachingErrors.ProfileNotFound;
        }

        var profile = await instructors.FindByMembershipAsync(membershipId, cancellationToken);
        if (profile is null)
        {
            return TeachingErrors.ProfileNotFound;
        }

        // Yuzde egitmen bazlidir: sonraki gec iptalin hangi basamaga denk geldigini
        // tek sorguda cozup tum satirlara yaziyoruz.
        var preview = await compensation.PreviewNextPenaltyPercentageAsync(
            profile.Id,
            cancellationToken);
        if (preview.IsFailure)
        {
            return preview.Error;
        }

        return Result.Success<IReadOnlyList<InstructorScheduleListItem>>(
            result
                .Select(item => item with { NextPenaltyPercentage = preview.Value })
                .ToList());
    }
}

public sealed class ListMyInstructorEarningsHandler(
    IInstructorQueries queries,
    ICurrentTenant currentTenant)
{
    public async Task<Result<IReadOnlyList<InstructorEarningListItem>>> Handle(
        DateTimeOffset? from,
        DateTimeOffset? until,
        CancellationToken cancellationToken)
    {
        if (currentTenant.MembershipId is not { } membershipId)
        {
            return TeachingErrors.OrganizationContextRequired;
        }

        var result = await queries.ListMyEarningsAsync(
            membershipId, from, until, cancellationToken);
        return result is null ? TeachingErrors.ProfileNotFound : Result.Success(result);
    }
}
