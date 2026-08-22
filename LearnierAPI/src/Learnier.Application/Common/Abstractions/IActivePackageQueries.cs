namespace Learnier.Application.Common.Abstractions;

/// <param name="PlanId">
/// Aboneligin dayandigi plan. Katalog ekrani "zaten abonesin" durumunu bununla
/// gosterir; plan adi uzerinden eslestirme ayni adli iki planda yanilirdi.
/// </param>
public sealed record ActivePackageAccess(
    Guid SubscriptionId,
    Guid PlanId,
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
