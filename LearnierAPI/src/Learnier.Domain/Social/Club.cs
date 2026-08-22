using Learnier.Domain.Catalog;
using Learnier.Domain.Common;

namespace Learnier.Domain.Social;

public sealed class Club : AggregateRoot, IAuditableEntity, ITenantScoped
{
    private readonly List<ClubRoom> _rooms = [];

    private Club()
    {
        Name = string.Empty;
        Description = string.Empty;
    }

    public Guid OrganizationId { get; private set; }

    public Guid SubjectId { get; private set; }

    public Subject Subject { get; private set; } = null!;

    public string Name { get; private set; }

    public string Description { get; private set; }

    public bool IsActive { get; private set; }

    public IReadOnlyCollection<ClubRoom> Rooms => _rooms.AsReadOnly();

    public DateTimeOffset CreatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    public Guid? UpdatedBy { get; set; }

    public static Club Create(
        Guid organizationId,
        Guid subjectId,
        string name,
        string? description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (organizationId == Guid.Empty)
        {
            throw new ArgumentException(
                "OrganizationId boş olamaz.",
                nameof(organizationId));
        }

        if (subjectId == Guid.Empty)
        {
            throw new ArgumentException(
                "SubjectId boş olamaz.",
                nameof(subjectId));
        }

        return new Club
        {
            OrganizationId = organizationId,
            SubjectId = subjectId,
            Name = name.Trim(),
            Description = description?.Trim() ?? string.Empty,
            IsActive = true
        };
    }

    public void Update(string name, string? description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Name = name.Trim();
        Description = description?.Trim() ?? string.Empty;
    }

    public void Close()
    {
        IsActive = false;
    }

    public void Open()
    {
        IsActive = true;
    }

    public ClubRoom AddRoom(string name, ClubRoomType type, int sortOrder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var normalizedName = name.Trim().ToLowerInvariant();
        var existing = _rooms.Find(room => room.Name == normalizedName);
        if (existing is not null)
        {
            return existing;
        }

        var room = ClubRoom.Create(Id, normalizedName, type, sortOrder);
        _rooms.Add(room);
        return room;
    }
}
