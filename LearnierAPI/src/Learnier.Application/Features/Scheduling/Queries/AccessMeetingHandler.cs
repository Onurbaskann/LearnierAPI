using Learnier.Application.Common.Abstractions;
using Learnier.Application.Common.Results;
using Learnier.Domain.Scheduling;

namespace Learnier.Application.Features.Scheduling.Queries;

public sealed class AccessMeetingHandler(
    ISchedulingQueries queries,
    ICurrentUser currentUser,
    ICurrentTenant currentTenant,
    IClock clock)
{
    private const int LearnerJoinWindowMinutes = 5;
    private const int InstructorHostWindowMinutes = 15;

    public async Task<Result<MeetingRoomAccessResult>> Handle(
        Guid meetingId,
        MeetingParticipantRole role,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Error.Unauthorized("common.unauthorized");
        }

        if (currentTenant.OrganizationId is not { } organizationId)
        {
            return SchedulingErrors.OrganizationContextRequired;
        }

        var meeting = await queries.FindMeetingAccessAsync(
            meetingId,
            organizationId,
            userId,
            currentTenant.MembershipId,
            cancellationToken);
        if (meeting is null)
        {
            return SchedulingErrors.MeetingNotFound;
        }

        var isAuthorized = role switch
        {
            MeetingParticipantRole.Attendee => meeting.HasActiveBooking,
            MeetingParticipantRole.Host => meeting.IsAssignedInstructor,
            _ => false
        };
        if (!isAuthorized)
        {
            return SchedulingErrors.MeetingAccessDenied;
        }

        if (meeting.MeetingStatus is not MeetingStatus.Ready)
        {
            return SchedulingErrors.MeetingNotReady;
        }

        if (meeting.SessionStatus is not (LessonSessionStatus.Confirmed
            or LessonSessionStatus.InProgress))
        {
            return SchedulingErrors.MeetingUnavailable;
        }

        var availableAt = meeting.StartsAt.AddMinutes(
            role is MeetingParticipantRole.Host
                ? -InstructorHostWindowMinutes
                : -LearnerJoinWindowMinutes);
        var now = clock.UtcNow;
        if (now < availableAt)
        {
            return SchedulingErrors.MeetingAccessTooEarly(availableAt);
        }

        if (now >= meeting.EndsAt)
        {
            return SchedulingErrors.MeetingAccessClosed;
        }

        // Sandbox'ta endpoint'in kendisi test odasidir; yeniden ayni adrese
        // yonlendirmek dongu olusturur. Gercek adapter geldiginde burada Zoom/Teams
        // adresi istemciye verilir.
        var redirectUrl = string.Equals(meeting.Provider, "sandbox", StringComparison.OrdinalIgnoreCase)
            ? null
            : role is MeetingParticipantRole.Host
                ? meeting.HostUrl
                : meeting.JoinUrl;

        return new MeetingRoomAccessResult(
            meeting.MeetingId,
            meeting.SessionId,
            meeting.Provider,
            meeting.ProviderMeetingId,
            role,
            meeting.StartsAt,
            meeting.EndsAt,
            redirectUrl);
    }
}
