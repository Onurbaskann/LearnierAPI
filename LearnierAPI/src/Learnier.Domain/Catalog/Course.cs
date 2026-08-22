using Learnier.Domain.Common;

namespace Learnier.Domain.Catalog;

/// <summary>
/// Egitim tanimi - "Baslangic Seviye Python" gibi.
/// </summary>
/// <remarks>
/// Bu bir takvim kaydi <b>degildir</b>. Belirli bir tarihte gerceklesen ders
/// <c>LessonSession</c> ile temsil edilir. Kaynak dokumanin 1. bolumundeki bu ayrim
/// korunmazsa mufredat ile takvim ic ice gecer ve ikisi de degistirilemez hale gelir.
/// </remarks>
public sealed class Course : AggregateRoot, IAuditableEntity, ITenantScoped
{
    private readonly List<CourseModule> _modules = [];

    private Course()
    {
        Title = string.Empty;
    }

    public Guid OrganizationId { get; private set; }

    public Guid SubjectId { get; private set; }

    /// <summary>Hedef seviye. Seviye ayrimi olmayan egitimlerde bos birakilir.</summary>
    public Guid? LevelId { get; private set; }

    public string Title { get; private set; }

    public string? Description { get; private set; }

    public CourseType CourseType { get; private set; }

    public CourseStatus Status { get; private set; }

    /// <summary>Bu egitimden uretilen oturumlarin varsayilan suresi.</summary>
    public int DefaultDurationMinutes { get; private set; }

    /// <summary>Oturumun acilmasi icin gereken en az katilimci sayisi.</summary>
    public int MinParticipants { get; private set; }

    public int MaxParticipants { get; private set; }

    public Subject Subject { get; private set; } = null!;

    public IReadOnlyCollection<CourseModule> Modules => _modules.AsReadOnly();

    public DateTimeOffset CreatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    public Guid? UpdatedBy { get; set; }

    public static Course Create(
        Guid organizationId,
        Guid subjectId,
        string title,
        CourseType courseType,
        int defaultDurationMinutes,
        int minParticipants,
        int maxParticipants,
        Guid? levelId = null,
        string? description = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(defaultDurationMinutes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(minParticipants);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxParticipants, minParticipants);

        return new Course
        {
            OrganizationId = organizationId,
            SubjectId = subjectId,
            LevelId = levelId,
            Title = title.Trim(),
            Description = description?.Trim(),
            CourseType = courseType,
            DefaultDurationMinutes = defaultDurationMinutes,
            MinParticipants = minParticipants,
            MaxParticipants = maxParticipants,
            Status = CourseStatus.Draft
        };
    }

    /// <summary>
    /// Egitimi yayina alir. Yalnizca taslak durumundaki egitim yayinlanabilir.
    /// </summary>
    public void Publish()
    {
        if (Status is not CourseStatus.Draft)
        {
            return;
        }

        Status = CourseStatus.Published;
    }

    public void Archive() => Status = CourseStatus.Archived;

    public CourseModule AddModule(string title, int sortOrder, string? description = null)
    {
        var module = CourseModule.Create(Id, title, sortOrder, description);
        _modules.Add(module);
        return module;
    }
}
