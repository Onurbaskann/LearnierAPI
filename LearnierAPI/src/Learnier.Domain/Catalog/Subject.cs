using Learnier.Domain.Common;

namespace Learnier.Domain.Catalog;

/// <summary>
/// Egitim alani: Ingilizce, Matematik, Yazilim gibi.
/// </summary>
/// <remarks>
/// <see cref="ParentSubjectId"/> tek seviyeli alt kirilim icindir - "Yazilim > Backend"
/// gibi. Organization'da oldugu gibi burada da closure table kurulmuyor;
/// kaynak dokumanin 14. bolumu bunu acikca onermiyor.
/// </remarks>
public sealed class Subject : AggregateRoot, IAuditableEntity, ITenantScoped
{
    private Subject()
    {
        Name = string.Empty;
        Slug = string.Empty;
    }

    public Guid OrganizationId { get; private set; }

    public Guid? ParentSubjectId { get; private set; }

    public string Name { get; private set; }

    public string Slug { get; private set; }

    public SubjectStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    public Guid? UpdatedBy { get; set; }

    public static Subject Create(
        Guid organizationId,
        string name,
        string slug,
        Guid? parentSubjectId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);

        return new Subject
        {
            OrganizationId = organizationId,
            Name = name.Trim(),
            Slug = slug.Trim().ToLowerInvariant(),
            ParentSubjectId = parentSubjectId,
            Status = SubjectStatus.Active
        };
    }

    public void Rename(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name.Trim();
    }

    public void Archive() => Status = SubjectStatus.Archived;
}
