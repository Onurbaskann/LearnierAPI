using Learnier.Domain.Catalog;
using Learnier.Domain.Common;

namespace Learnier.Domain.Scheduling;

/// <summary>
/// Belirli bir ogrenci toplulugu - "Python Sali-Persembe Grubu" gibi.
/// </summary>
/// <remarks>
/// Sabit kadrolu sinif ihtiyaci icindir. Serbest katilimli derslerde ogrencinin
/// gruba kaydolmasi gerekmez; dogrudan oturuma rezervasyon yapar. Bu yuzden
/// <c>LessonSession.ClassGroupId</c> zorunlu degildir.
/// </remarks>
public sealed class ClassGroup : AggregateRoot, IAuditableEntity, ITenantScoped
{
    private readonly List<ClassGroupMember> _members = [];

    private ClassGroup()
    {
        Name = string.Empty;
    }

    public Guid OrganizationId { get; private set; }

    public Guid CourseId { get; private set; }

    public string Name { get; private set; }

    public ClassGroupDeliveryType DeliveryType { get; private set; }

    /// <summary>Donem baslangici. Serbest havuzlarda bos birakilabilir.</summary>
    public DateOnly? StartsOn { get; private set; }

    public DateOnly? EndsOn { get; private set; }

    public int Capacity { get; private set; }

    public ClassGroupStatus Status { get; private set; }

    public Course Course { get; private set; } = null!;

    public IReadOnlyCollection<ClassGroupMember> Members => _members.AsReadOnly();

    public DateTimeOffset CreatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    public Guid? UpdatedBy { get; set; }

    public static ClassGroup Create(
        Guid organizationId,
        Guid courseId,
        string name,
        ClassGroupDeliveryType deliveryType,
        int capacity,
        DateOnly? startsOn = null,
        DateOnly? endsOn = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);

        if (startsOn is not null && endsOn is not null && endsOn < startsOn)
        {
            throw new ArgumentException("Donem bitisi baslangictan once olamaz.", nameof(endsOn));
        }

        return new ClassGroup
        {
            OrganizationId = organizationId,
            CourseId = courseId,
            Name = name.Trim(),
            DeliveryType = deliveryType,
            Capacity = capacity,
            StartsOn = startsOn,
            EndsOn = endsOn,
            Status = ClassGroupStatus.Planned
        };
    }

    /// <summary>
    /// Ogrenciyi sinifa kaydeder.
    /// </summary>
    /// <remarks>
    /// Buradaki kapasite kontrolu yalnizca bellekte yuklenmis uyeler icin gecerlidir;
    /// es zamanli kayitlarda tek dogru koruma veritabani seviyesindeki islem ve
    /// satir kilididir. Rezervasyondaki ayni tuzak icin <c>LessonSession</c> notuna bak.
    /// </remarks>
    public ClassGroupMember Enroll(Guid learnerUserId, DateTimeOffset enrolledAt)
    {
        var existing = _members.Find(m => m.LearnerUserId == learnerUserId);
        if (existing is not null)
        {
            return existing;
        }

        var member = ClassGroupMember.Create(Id, learnerUserId, enrolledAt);
        _members.Add(member);
        return member;
    }

    public void Activate() => Status = ClassGroupStatus.Active;

    public void Complete() => Status = ClassGroupStatus.Completed;

    public void Cancel() => Status = ClassGroupStatus.Cancelled;
}
