using Learnier.Domain.Social;
using Shouldly;

namespace Learnier.UnitTests.Domain;

public sealed class FriendshipTests
{
    [Fact]
    public void Request_StoresUserPairInCanonicalOrder()
    {
        var left = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff");
        var right = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var requestedAt = DateTimeOffset.UtcNow;

        var friendship = Friendship.Request(left, right, requestedAt);

        friendship.FirstUserId.ShouldBe(right);
        friendship.SecondUserId.ShouldBe(left);
        friendship.RequestedByUserId.ShouldBe(left);
        friendship.Status.ShouldBe(FriendshipStatus.Pending);
        friendship.RequestedAt.ShouldBe(requestedAt);
        friendship.RespondedAt.ShouldBeNull();
    }

    [Fact]
    public void Request_RejectsSelfRequest()
    {
        var userId = Guid.NewGuid();

        Should.Throw<ArgumentException>(() =>
            Friendship.Request(userId, userId, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Accept_AllowsOnlyRequestTarget()
    {
        var requesterId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        var friendship = Friendship.Request(requesterId, targetId, DateTimeOffset.UtcNow);
        var respondedAt = DateTimeOffset.UtcNow.AddMinutes(1);

        friendship.Accept(targetId, respondedAt);

        friendship.Status.ShouldBe(FriendshipStatus.Accepted);
        friendship.RespondedAt.ShouldBe(respondedAt);
    }

    [Fact]
    public void Accept_RejectsRequesterResponse()
    {
        var requesterId = Guid.NewGuid();
        var friendship = Friendship.Request(requesterId, Guid.NewGuid(), DateTimeOffset.UtcNow);

        Should.Throw<InvalidOperationException>(() =>
            friendship.Accept(requesterId, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void DeclinedRequest_CanBeSentAgainByEitherParticipant()
    {
        var firstUserId = Guid.NewGuid();
        var secondUserId = Guid.NewGuid();
        var friendship = Friendship.Request(firstUserId, secondUserId, DateTimeOffset.UtcNow);
        friendship.Decline(secondUserId, DateTimeOffset.UtcNow.AddMinutes(1));
        var requestedAgainAt = DateTimeOffset.UtcNow.AddMinutes(2);

        friendship.RequestAgain(secondUserId, requestedAgainAt);

        friendship.Status.ShouldBe(FriendshipStatus.Pending);
        friendship.RequestedByUserId.ShouldBe(secondUserId);
        friendship.RequestedAt.ShouldBe(requestedAgainAt);
        friendship.RespondedAt.ShouldBeNull();
    }
}
