namespace Learnier.Application.Common.Abstractions;

public sealed record ActivePackageAccess(
    Guid SubscriptionId,
    string PlanName,
    Guid SubjectId,
    string SubjectName,
    DateTimeOffset StartsAt,
    DateTimeOffset CurrentPeriodEnd,
    int RemainingCredits,
    int TotalCredits,
    int LessonsPerWeek,
    int DurationMonths,
    int LessonDurationMinutes);

public interface IActivePackageQueries
{
    Task<IReadOnlyList<ActivePackageAccess>> ListForUserAsync(
        Guid userId,
        CancellationToken cancellationToken);
}
