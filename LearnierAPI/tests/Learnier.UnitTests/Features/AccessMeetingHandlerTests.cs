using Learnier.Application.Common.Abstractions;
using Learnier.Application.Features.Scheduling.Queries;
using Learnier.Domain.Scheduling;
using NSubstitute;
using Shouldly;

namespace Learnier.UnitTests.Features;

public sealed class AccessMeetingHandlerTests
{
    [Fact]
    public async Task Learner_WithReservation_CanJoinInsideFiveMinuteWindow()
    {
        var now = DateTimeOffset.UtcNow;
        var meetingId = Guid.NewGuid();
        var snapshot = CreateSnapshot(meetingId, now.AddMinutes(4)) with
        {
            HasActiveBooking = true
        };
        var (handler, _, _, _) = CreateHandler(snapshot, now);

        var result = await handler.Handle(
            meetingId,
            MeetingParticipantRole.Attendee,
            CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Role.ShouldBe(MeetingParticipantRole.Attendee);
        result.Value.Provider.ShouldBe("sandbox");
        result.Value.RedirectUrl.ShouldBeNull();
    }

    [Fact]
    public async Task AssignedInstructor_CanHostInsideFifteenMinuteWindow()
    {
        var now = DateTimeOffset.UtcNow;
        var meetingId = Guid.NewGuid();
        var snapshot = CreateSnapshot(meetingId, now.AddMinutes(14)) with
        {
            IsAssignedInstructor = true
        };
        var (handler, _, _, _) = CreateHandler(snapshot, now);

        var result = await handler.Handle(
            meetingId,
            MeetingParticipantRole.Host,
            CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Role.ShouldBe(MeetingParticipantRole.Host);
    }

    [Theory]
    [InlineData(MeetingParticipantRole.Host)]
    [InlineData(MeetingParticipantRole.Attendee)]
    public async Task User_CannotUseRoleThatDoesNotBelongToThem(
        MeetingParticipantRole requestedRole)
    {
        var now = DateTimeOffset.UtcNow;
        var meetingId = Guid.NewGuid();
        var snapshot = CreateSnapshot(meetingId, now.AddMinutes(1)) with
        {
            HasActiveBooking = requestedRole is MeetingParticipantRole.Host,
            IsAssignedInstructor = requestedRole is MeetingParticipantRole.Attendee
        };
        var (handler, _, _, _) = CreateHandler(snapshot, now);

        var result = await handler.Handle(meetingId, requestedRole, CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("scheduling.meeting_access_denied");
    }

    [Fact]
    public async Task Learner_CannotJoinBeforeFiveMinuteWindow()
    {
        var now = DateTimeOffset.UtcNow;
        var meetingId = Guid.NewGuid();
        var snapshot = CreateSnapshot(meetingId, now.AddMinutes(6)) with
        {
            HasActiveBooking = true
        };
        var (handler, _, _, _) = CreateHandler(snapshot, now);

        var result = await handler.Handle(
            meetingId,
            MeetingParticipantRole.Attendee,
            CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("scheduling.meeting_access_too_early");
    }

    private static (
        AccessMeetingHandler Handler,
        ISchedulingQueries Queries,
        ICurrentUser CurrentUser,
        ICurrentTenant CurrentTenant) CreateHandler(
        MeetingAccessSnapshot snapshot,
        DateTimeOffset now)
    {
        var queries = Substitute.For<ISchedulingQueries>();
        var currentUser = Substitute.For<ICurrentUser>();
        var currentTenant = Substitute.For<ICurrentTenant>();
        var clock = Substitute.For<IClock>();
        var userId = Guid.NewGuid();
        var organizationId = Guid.NewGuid();
        var membershipId = Guid.NewGuid();

        currentUser.UserId.Returns(userId);
        currentTenant.OrganizationId.Returns(organizationId);
        currentTenant.MembershipId.Returns(membershipId);
        clock.UtcNow.Returns(now);
        queries.FindMeetingAccessAsync(
                snapshot.MeetingId,
                organizationId,
                userId,
                membershipId,
                Arg.Any<CancellationToken>())
            .Returns(snapshot);

        return (
            new AccessMeetingHandler(queries, currentUser, currentTenant, clock),
            queries,
            currentUser,
            currentTenant);
    }

    private static MeetingAccessSnapshot CreateSnapshot(
        Guid meetingId,
        DateTimeOffset startsAt)
        => new(
            meetingId,
            Guid.NewGuid(),
            "sandbox",
            $"sandbox-meeting-{meetingId:N}",
            MeetingStatus.Ready,
            LessonSessionStatus.Confirmed,
            startsAt,
            startsAt.AddHours(1),
            $"http://localhost/api/v1/meetings/sandbox/{meetingId}/join",
            $"http://localhost/api/v1/meetings/sandbox/{meetingId}/host",
            false,
            false);
}
