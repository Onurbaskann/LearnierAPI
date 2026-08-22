using Learnier.Domain.Scheduling;
using Shouldly;

namespace Learnier.UnitTests.Domain;

public sealed class MeetingTests
{
    [Fact]
    public void Meeting_ShouldMoveFromRequestToReady()
    {
        var now = DateTimeOffset.UtcNow;
        var meeting = Meeting.Request(
            Guid.NewGuid(),
            Guid.NewGuid(),
            " Sandbox ",
            now.AddHours(1),
            now.AddHours(2));

        meeting.Provider.ShouldBe("sandbox");
        meeting.Status.ShouldBe(MeetingStatus.Pending);

        meeting.StartProvisioning();
        meeting.MarkReady(
            "provider-meeting-1",
            "https://meeting.example/join/1",
            "https://meeting.example/host/1",
            now);

        meeting.Status.ShouldBe(MeetingStatus.Ready);
        meeting.ProvisioningAttemptCount.ShouldBe(1);
        meeting.JoinUrl.ShouldNotBe(meeting.HostUrl);
        meeting.ProvisionedAt.ShouldBe(now);
    }

    [Fact]
    public void FailedMeeting_ShouldBeRetryable()
    {
        var now = DateTimeOffset.UtcNow;
        var meeting = Meeting.Request(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "sandbox",
            now.AddHours(1),
            now.AddHours(2));

        meeting.StartProvisioning();
        meeting.MarkFailed("gecici saglayici hatasi");
        meeting.StartProvisioning();

        meeting.Status.ShouldBe(MeetingStatus.Provisioning);
        meeting.ProvisioningAttemptCount.ShouldBe(2);
        meeting.LastError.ShouldBeNull();
    }

    [Fact]
    public void Meeting_ShouldRejectInvalidTimeRangeAndInvalidTransition()
    {
        var now = DateTimeOffset.UtcNow;

        Should.Throw<ArgumentException>(() => Meeting.Request(
            Guid.NewGuid(), Guid.NewGuid(), "sandbox", now, now));

        var meeting = Meeting.Request(
            Guid.NewGuid(), Guid.NewGuid(), "sandbox", now, now.AddHours(1));

        Should.Throw<InvalidOperationException>(() => meeting.MarkReady(
            "provider-meeting-1",
            "https://meeting.example/join/1",
            "https://meeting.example/host/1",
            now));
    }

    [Fact]
    public void ReadyMeeting_ShouldTrackProviderCancellation()
    {
        var now = DateTimeOffset.UtcNow;
        var meeting = Meeting.Request(
            Guid.NewGuid(), Guid.NewGuid(), "sandbox", now, now.AddHours(1));
        meeting.StartProvisioning();
        meeting.MarkReady(
            "provider-meeting-1",
            "https://meeting.example/join/1",
            "https://meeting.example/host/1",
            now);

        meeting.Cancel(now.AddMinutes(1));
        meeting.StartCancellationAttempt();
        meeting.MarkProviderCancelled(now.AddMinutes(2));

        meeting.Status.ShouldBe(MeetingStatus.Cancelled);
        meeting.CancellationAttemptCount.ShouldBe(1);
        meeting.ProviderCancelledAt.ShouldBe(now.AddMinutes(2));
        meeting.LastError.ShouldBeNull();
    }
}
