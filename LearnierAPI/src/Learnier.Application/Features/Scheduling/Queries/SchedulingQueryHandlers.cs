using Learnier.Application.Common.Abstractions;
using Learnier.Application.Common.Models;
using Learnier.Application.Common.Results;
using Learnier.Domain.Scheduling;

namespace Learnier.Application.Features.Scheduling.Queries;

public sealed class ListClassGroupsHandler(
    ISchedulingQueries queries,
    ICurrentTenant currentTenant)
{
    public async Task<Result<PagedResult<ClassGroupListItem>>> Handle(
        PageRequest page,
        Guid? courseId,
        ClassGroupStatus? status,
        CancellationToken cancellationToken)
    {
        if (!currentTenant.HasTenant)
        {
            return SchedulingErrors.OrganizationContextRequired;
        }

        return await queries.ListClassGroupsAsync(page, courseId, status, cancellationToken);
    }
}

public sealed class GetClassGroupDetailHandler(
    ISchedulingQueries queries,
    ICurrentTenant currentTenant)
{
    public async Task<Result<ClassGroupDetail>> Handle(
        Guid classGroupId,
        CancellationToken cancellationToken)
    {
        if (!currentTenant.HasTenant)
        {
            return SchedulingErrors.OrganizationContextRequired;
        }

        var detail = await queries.FindClassGroupDetailAsync(classGroupId, cancellationToken);
        return detail is null ? SchedulingErrors.ClassGroupNotFound : Result.Success(detail);
    }
}

public sealed class ListSessionsHandler(
    ISchedulingQueries queries,
    ICurrentTenant currentTenant)
{
    public async Task<Result<PagedResult<SessionListItem>>> Handle(
        PageRequest page,
        Guid? courseId,
        DateTimeOffset? from,
        DateTimeOffset? until,
        LessonSessionStatus? status,
        CancellationToken cancellationToken)
    {
        if (!currentTenant.HasTenant)
        {
            return SchedulingErrors.OrganizationContextRequired;
        }

        return await queries.ListSessionsAsync(page, courseId, from, until, status, cancellationToken);
    }
}

public sealed class GetSessionDetailHandler(
    ISchedulingQueries queries,
    ICurrentTenant currentTenant)
{
    public async Task<Result<SessionDetail>> Handle(
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        if (!currentTenant.HasTenant)
        {
            return SchedulingErrors.OrganizationContextRequired;
        }

        var detail = await queries.FindSessionDetailAsync(sessionId, cancellationToken);
        return detail is null ? SchedulingErrors.SessionNotFound : Result.Success(detail);
    }
}

public sealed class ListMyBookingsHandler(
    ISchedulingQueries queries,
    ICurrentTenant currentTenant,
    ICurrentUser currentUser,
    IClock clock)
{
    public async Task<Result<PagedResult<LearnerBookingListItem>>> Handle(
        PageRequest page,
        DateTimeOffset? from,
        DateTimeOffset? until,
        BookingStatus? status,
        CancellationToken cancellationToken)
    {
        if (!currentTenant.HasTenant)
        {
            return SchedulingErrors.OrganizationContextRequired;
        }

        if (currentUser.UserId is not { } userId)
        {
            return Error.Unauthorized("common.unauthorized");
        }

        return await queries.ListLearnerBookingsAsync(
            page, userId, from, until, status, clock.UtcNow, cancellationToken);
    }
}
