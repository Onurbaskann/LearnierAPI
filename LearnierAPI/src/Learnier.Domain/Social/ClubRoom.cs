using Learnier.Domain.Common;

namespace Learnier.Domain.Social;

public sealed class ClubRoom : Entity, IAuditableEntity
{
    private ClubRoom()
    {
        Name = string.Empty;
    }

    public Guid ClubId { get; private set; }

    public string Name { get; private set; }

    public ClubRoomType Type { get; private set; }

    public int SortOrder { get; private set; }

    public bool IsActive { get; private set; }

    public Club Club { get; private set; } = null!;

    public DateTimeOffset CreatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    public Guid? UpdatedBy { get; set; }

    internal static ClubRoom Create(Guid clubId, string name, ClubRoomType type, int sortOrder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentOutOfRangeException.ThrowIfNegative(sortOrder);

        return new ClubRoom
        {
            ClubId = clubId,
            Name = name.Trim().ToLowerInvariant(),
            Type = type,
            SortOrder = sortOrder,
            IsActive = true
        };
    }

    public void Close() => IsActive = false;
}

public enum ClubRoomType
{
    Text,
    Voice
}
