using Learnier.Domain.Catalog;
using Learnier.Domain.Common;
using Learnier.Domain.Identity;
using Learnier.Domain.Scheduling;

namespace Learnier.Domain.Progress;

/// <summary>
/// Ogrencinin bir mufredat konusunu tamamlamasi.
/// </summary>
/// <remarks>
/// <see cref="SessionId"/> bos olabilir: konu, derse katilim disinda egitmen
/// isaretlemesiyle de tamamlanmis sayilabilir.
/// </remarks>
public sealed class LessonCompletion : Entity, IAuditableEntity
{
    private LessonCompletion()
    {
    }

    public Guid LearnerUserId { get; private set; }

    public Guid CourseLessonId { get; private set; }

    public Guid? SessionId { get; private set; }

    public DateTimeOffset CompletedAt { get; private set; }

    public LessonCompletionSource CompletionSource { get; private set; }

    public User Learner { get; private set; } = null!;

    public CourseLesson CourseLesson { get; private set; } = null!;

    public LessonSession? Session { get; private set; }

    public DateTimeOffset CreatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    public Guid? UpdatedBy { get; set; }

    public static LessonCompletion Create(
        Guid learnerUserId,
        Guid courseLessonId,
        DateTimeOffset completedAt,
        LessonCompletionSource completionSource,
        Guid? sessionId = null)
        => new()
        {
            LearnerUserId = learnerUserId,
            CourseLessonId = courseLessonId,
            CompletedAt = completedAt,
            CompletionSource = completionSource,
            SessionId = sessionId
        };
}
