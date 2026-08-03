using Learnier.Domain.Common;

namespace Learnier.Domain.Catalog;

/// <summary>
/// Egitimin ana bolumu. Icindeki ders basliklari <see cref="CourseLesson"/> ile tutulur.
/// </summary>
public sealed class CourseModule : Entity, IAuditableEntity
{
    private readonly List<CourseLesson> _lessons = [];

    private CourseModule()
    {
        Title = string.Empty;
    }

    public Guid CourseId { get; private set; }

    public string Title { get; private set; }

    public string? Description { get; private set; }

    public int SortOrder { get; private set; }

    public Course Course { get; private set; } = null!;

    public IReadOnlyCollection<CourseLesson> Lessons => _lessons.AsReadOnly();

    public DateTimeOffset CreatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    public Guid? UpdatedBy { get; set; }

    internal static CourseModule Create(Guid courseId, string title, int sortOrder, string? description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);

        return new CourseModule
        {
            CourseId = courseId,
            Title = title.Trim(),
            Description = description?.Trim(),
            SortOrder = sortOrder
        };
    }

    public CourseLesson AddLesson(
        string title,
        int sortOrder,
        int estimatedDurationMinutes,
        string? description = null)
    {
        var lesson = CourseLesson.Create(Id, title, sortOrder, estimatedDurationMinutes, description);
        _lessons.Add(lesson);
        return lesson;
    }
}
