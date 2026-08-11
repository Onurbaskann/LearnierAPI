using Learnier.Domain.Common;
using Learnier.Domain.Identity;

namespace Learnier.Domain.Social;

public enum FriendshipStatus
{
    Pending,
    Accepted,
    Declined
}

/// <summary>Iki kullanici arasindaki arkadaslik istegi ve sonucunu tutar.</summary>
public sealed class Friendship : AggregateRoot
{
    private Friendship()
    {
    }

    public Guid FirstUserId { get; private set; }
    public Guid SecondUserId { get; private set; }
    public Guid RequestedByUserId { get; private set; }
    public FriendshipStatus Status { get; private set; }
    public DateTimeOffset RequestedAt { get; private set; }
    public DateTimeOffset? RespondedAt { get; private set; }
    public User FirstUser { get; private set; } = null!;
    public User SecondUser { get; private set; } = null!;

    public static Friendship Request(
        Guid requesterUserId,
        Guid targetUserId,
        DateTimeOffset requestedAt)
    {
        if (requesterUserId == targetUserId)
        {
            throw new ArgumentException("Kullanici kendisine arkadaslik istegi gonderemez.");
        }

        var (firstUserId, secondUserId) = OrderPair(requesterUserId, targetUserId);
        return new Friendship
        {
            FirstUserId = firstUserId,
            SecondUserId = secondUserId,
            RequestedByUserId = requesterUserId,
            Status = FriendshipStatus.Pending,
            RequestedAt = requestedAt
        };
    }

    public bool Includes(Guid userId) => FirstUserId == userId || SecondUserId == userId;

    public Guid OtherUserId(Guid userId)
    {
        if (FirstUserId == userId)
        {
            return SecondUserId;
        }

        if (SecondUserId == userId)
        {
            return FirstUserId;
        }

        throw new ArgumentException("Kullanici bu arkadasligin tarafi degil.", nameof(userId));
    }

    public void Accept(Guid respondingUserId, DateTimeOffset respondedAt)
    {
        EnsureCanRespond(respondingUserId);
        Status = FriendshipStatus.Accepted;
        RespondedAt = respondedAt;
    }

    public void Decline(Guid respondingUserId, DateTimeOffset respondedAt)
    {
        EnsureCanRespond(respondingUserId);
        Status = FriendshipStatus.Declined;
        RespondedAt = respondedAt;
    }

    public void RequestAgain(Guid requesterUserId, DateTimeOffset requestedAt)
    {
        if (Status is not FriendshipStatus.Declined || !Includes(requesterUserId))
        {
            throw new InvalidOperationException("Arkadaslik istegi yeniden gonderilemez.");
        }

        RequestedByUserId = requesterUserId;
        Status = FriendshipStatus.Pending;
        RequestedAt = requestedAt;
        RespondedAt = null;
    }

    private void EnsureCanRespond(Guid respondingUserId)
    {
        if (Status is not FriendshipStatus.Pending
            || !Includes(respondingUserId)
            || RequestedByUserId == respondingUserId)
        {
            throw new InvalidOperationException("Arkadaslik istegi yanitlanamaz.");
        }
    }

    private static (Guid First, Guid Second) OrderPair(Guid left, Guid right)
        => left.CompareTo(right) < 0 ? (left, right) : (right, left);
}
