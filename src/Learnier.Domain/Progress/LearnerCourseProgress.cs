using Learnier.Domain.Catalog;
using Learnier.Domain.Common;
using Learnier.Domain.Identity;

namespace Learnier.Domain.Progress;

/// <summary>
/// Ogrencinin bir egitimdeki ilerlemesi.
/// </summary>
/// <remarks>
/// <see cref="CompletionPercentage"/> aslinda <c>LessonCompletion</c> kayitlarindan
/// turetilebilir. Burada saklanmasinin tek gerekcesi panel performansi: her acilista
/// tum tamamlama kayitlarini saymak yerine hazir deger okunur. Dogruluk kaynagi
/// yine tamamlama kayitlaridir; bu satir onlardan yeniden uretilebilir.
/// </remarks>
public sealed class LearnerCourseProgress : Entity, IAuditableEntity
{
    private LearnerCourseProgress()
    {
    }

    public Guid LearnerUserId { get; private set; }

    public Guid CourseId { get; private set; }

    public Guid? CurrentLevelId { get; private set; }

    /// <summary>0 ile 100 arasinda tamamlanma orani.</summary>
    public decimal CompletionPercentage { get; private set; }

    public DateTimeOffset StartedAt { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    public User Learner { get; private set; } = null!;

    public Course Course { get; private set; } = null!;

    public Level? CurrentLevel { get; private set; }

    public DateTimeOffset CreatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    public Guid? UpdatedBy { get; set; }

    public static LearnerCourseProgress Start(
        Guid learnerUserId,
        Guid courseId,
        DateTimeOffset startedAt,
        Guid? currentLevelId = null)
        => new()
        {
            LearnerUserId = learnerUserId,
            CourseId = courseId,
            CurrentLevelId = currentLevelId,
            StartedAt = startedAt,
            CompletionPercentage = 0m
        };

    /// <summary>
    /// Tamamlanma oranini gunceller; oran 100'e ulastiginda egitim tamamlanmis sayilir.
    /// </summary>
    public void UpdateProgress(decimal completionPercentage, DateTimeOffset updatedAt)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(completionPercentage);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(completionPercentage, 100m);

        CompletionPercentage = completionPercentage;

        if (completionPercentage is 100m && CompletedAt is null)
        {
            CompletedAt = updatedAt;
        }
    }

    public void MoveToLevel(Guid levelId) => CurrentLevelId = levelId;
}
