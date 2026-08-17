using Learnier.Domain.Common;
using Learnier.Domain.Identity;

namespace Learnier.Domain.Social;

public sealed class ClubMessage : Entity, IAuditableEntity
{
    private ClubMessage()
    {
        Body = string.Empty;
    }

    public Guid RoomId { get; private set; }

    public Guid AuthorUserId { get; private set; }

    public string Body { get; private set; }

    public ClubRoom Room { get; private set; } = null!;

    public User AuthorUser { get; private set; } = null!;

    public DateTimeOffset CreatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    public Guid? UpdatedBy { get; set; }

    public static ClubMessage Create(Guid roomId, Guid authorUserId, string body)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(body);

        return new ClubMessage
        {
            RoomId = roomId,
            AuthorUserId = authorUserId,
            Body = body.Trim()
        };
    }
}
