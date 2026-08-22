namespace Learnier.Domain.Scheduling;

public enum MeetingStatus
{
    Pending,
    Provisioning,
    Ready,
    Failed,
    Cancelled,
    Ended
}

public enum MeetingParticipantRole
{
    Host,
    CoHost,
    Attendee
}
