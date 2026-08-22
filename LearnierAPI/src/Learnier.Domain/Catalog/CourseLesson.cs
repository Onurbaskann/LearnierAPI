using Learnier.Domain.Common;

namespace Learnier.Domain.Catalog;

/// <summary>
/// Mufredattaki ders basligi.
/// </summary>
/// <remarks>
/// Randevu degildir: "Present Perfect" bir mufredat konusudur, 14 Agustos 20.00'daki
/// ders ise <c>LessonSession</c> kaydidir. Oturum, isledigi konuyu
/// <c>CourseLessonId</c> ile bu tabloya baglar.
/// </remarks>
public sealed class CourseLesson : Entity, IAuditableEntity
{
    private CourseLesson()
    {
        Title = string.Empty;
    }

    public Guid ModuleId { get; private set; }

    public string Title { get; private set; }

    public string? Description { get; private set; }

    public int EstimatedDurationMinutes { get; private set; }

    public int SortOrder { get; private set; }

    public CourseModule Module { get; private set; } = null!;

    public DateTimeOffset CreatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    public Guid? UpdatedBy { get; set; }

    internal static CourseLesson Create(
        Guid moduleId,
        string title,
        int sortOrder,
        int estimatedDurationMinutes,
        string? description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(estimatedDurationMinutes);

        return new CourseLesson
        {
            ModuleId = moduleId,
            Title = title.Trim(),
            Description = description?.Trim(),
            SortOrder = sortOrder,
            EstimatedDurationMinutes = estimatedDurationMinutes
        };
    }
}
