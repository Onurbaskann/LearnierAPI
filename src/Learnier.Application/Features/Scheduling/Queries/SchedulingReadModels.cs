using Learnier.Domain.Scheduling;

namespace Learnier.Application.Features.Scheduling.Queries;

public sealed record ClassGroupListItem(
    Guid Id,
    Guid CourseId,
    string CourseTitle,
    string Name,
    ClassGroupDeliveryType DeliveryType,
    DateOnly? StartsOn,
    DateOnly? EndsOn,
    int Capacity,
    ClassGroupStatus Status,
    int ActiveMemberCount);

public sealed record ClassGroupDetail(
    Guid Id,
    Guid CourseId,
    string CourseTitle,
    string Name,
    ClassGroupDeliveryType DeliveryType,
    DateOnly? StartsOn,
    DateOnly? EndsOn,
    int Capacity,
    ClassGroupStatus Status,
    IReadOnlyList<ClassGroupMemberDetail> Members);

public sealed record ClassGroupMemberDetail(
    Guid Id,
    Guid LearnerUserId,
    string FirstName,
    string LastName,
    ClassGroupMemberStatus Status,
    DateTimeOffset EnrolledAt,
    DateTimeOffset? LeftAt);

public sealed record SessionListItem(
    Guid Id,
    Guid CourseId,
    string CourseTitle,
    Guid? ClassGroupId,
    SessionType SessionType,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    int Capacity,
    int MinimumParticipants,
    LessonSessionStatus Status,
    int ReservedSeatCount,
    int WaitlistedCount);

public sealed record SessionDetail(
    Guid Id,
    Guid CourseId,
    string CourseTitle,
    Guid? ClassGroupId,
    Guid? CourseLessonId,
    SessionType SessionType,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    int Capacity,
    int MinimumParticipants,
    LessonSessionStatus Status,
    string? MeetingProvider,
    string? MeetingReference,
    DateTimeOffset? BookingOpensAt,
    DateTimeOffset? BookingClosesAt,
    DateTimeOffset? CancellationDeadlineAt,
    string? CancellationReason,
    int ReservedSeatCount,
    int WaitlistedCount,
    IReadOnlyList<SessionInstructorDetail> Instructors);

/// <param name="UserId">
/// Egitmenin kullanici kimligi. Birebir mesajlasma egitmen profili uzerinden
/// degil kullanici uzerinden yurudugu icin listede tasinir.
/// </param>
public sealed record SessionInstructorDetail(
    Guid InstructorProfileId,
    Guid UserId,
    string FirstName,
    string LastName,
    SessionInstructorRole Role);

/// <param name="CancellationDeadlineAt">
/// Bu ana kadar iptal edilirse ders hakki iade edilir. Bos ise iade kosulsuzdur.
/// </param>
/// <param name="CanCancel">Ogrenci bu rezervasyonu su anda iptal edebilir mi.</param>
/// <param name="WillRefundIfCancelled">
/// Su anda iptal edilirse hak iade edilir mi. Arayuz onay metnini buna gore kurar.
/// </param>
public sealed record LearnerBookingListItem(
    Guid Id,
    BookingStatus Status,
    DateTimeOffset BookedAt,
    Guid SessionId,
    Guid CourseId,
    string CourseTitle,
    string SubjectName,
    SessionType SessionType,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    LessonSessionStatus SessionStatus,
    string? MeetingProvider,
    string? MeetingReference,
    DateTimeOffset? CancellationDeadlineAt,
    bool CanCancel,
    bool WillRefundIfCancelled,
    IReadOnlyList<SessionInstructorDetail> Instructors);

/// <summary>Meeting erisim kurallarinin degerlendirildigi, disariya acilmayan projeksiyon.</summary>
public sealed record MeetingAccessSnapshot(
    Guid MeetingId,
    Guid SessionId,
    string Provider,
    string? ProviderMeetingId,
    MeetingStatus MeetingStatus,
    LessonSessionStatus SessionStatus,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    string? JoinUrl,
    string? HostUrl,
    bool HasActiveBooking,
    bool IsAssignedInstructor);

public sealed record MeetingRoomAccessResult(
    Guid MeetingId,
    Guid SessionId,
    string Provider,
    string? ProviderMeetingId,
    MeetingParticipantRole Role,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    string? RedirectUrl);
