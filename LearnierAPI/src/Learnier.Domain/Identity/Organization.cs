using Learnier.Domain.Common;

namespace Learnier.Domain.Identity;

/// <summary>
/// Platformdaki kurum: egitim saglayici, kurumsal musteri, okul veya sube.
/// </summary>
/// <remarks>
/// Kurum-sube iliskisi <see cref="ParentOrganizationId"/> ile kuruluyor.
/// Kaynak dokumanin 2. bolumu geregi ayri bir agac veya closure table kurulmuyor:
/// tek seviyeli sube yapisi mevcut ihtiyaci karsiliyor.
/// </remarks>
public sealed class Organization : AggregateRoot, IAuditableEntity
{
    private readonly List<OrganizationMembership> _memberships = [];

    private Organization()
    {
        Name = string.Empty;
        Slug = string.Empty;
        TimeZoneId = string.Empty;
        DefaultCurrency = string.Empty;
    }

    public Guid? ParentOrganizationId { get; private set; }

    public string Name { get; private set; }

    /// <summary>URL ve kiraci anahtari olarak kullanilan benzersiz kisa ad.</summary>
    public string Slug { get; private set; }

    public OrganizationType OrganizationType { get; private set; }

    public OrganizationStatus Status { get; private set; }

    /// <summary>IANA saat dilimi kimligi, ornegin <c>Europe/Istanbul</c>.</summary>
    public string TimeZoneId { get; private set; }

    /// <summary>ISO 4217 para birimi kodu, ornegin <c>TRY</c>.</summary>
    public string DefaultCurrency { get; private set; }

    public IReadOnlyCollection<OrganizationMembership> Memberships => _memberships.AsReadOnly();

    public DateTimeOffset CreatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    public Guid? UpdatedBy { get; set; }

    public static Organization Create(
        string name,
        string slug,
        OrganizationType organizationType,
        string timeZoneId,
        string defaultCurrency,
        Guid? parentOrganizationId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);
        ArgumentException.ThrowIfNullOrWhiteSpace(timeZoneId);
        ArgumentException.ThrowIfNullOrWhiteSpace(defaultCurrency);

        return new Organization
        {
            Name = name.Trim(),
            Slug = slug.Trim().ToLowerInvariant(),
            OrganizationType = organizationType,
            TimeZoneId = timeZoneId,
            DefaultCurrency = defaultCurrency.Trim().ToUpperInvariant(),
            ParentOrganizationId = parentOrganizationId,
            Status = OrganizationStatus.Active
        };
    }

    /// <summary>
    /// Kullaniciyi bu organizasyona uye yapar.
    /// </summary>
    public OrganizationMembership AddMember(Guid userId, MembershipStatus status, DateTimeOffset? joinedAt)
    {
        var membership = OrganizationMembership.Create(Id, userId, status, joinedAt);
        _memberships.Add(membership);
        return membership;
    }

    public void Suspend() => Status = OrganizationStatus.Suspended;
}
