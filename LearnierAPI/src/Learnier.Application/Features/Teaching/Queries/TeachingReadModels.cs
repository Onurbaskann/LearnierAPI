using Learnier.Domain.Teaching;
using Learnier.Domain.Scheduling;

namespace Learnier.Application.Features.Teaching.Queries;

public sealed record InstructorListItem(
    Guid Id,
    Guid MembershipId,
    string FirstName,
    string LastName,
    string? Headline,
    InstructorStatus Status,
    string TimeZoneId,
    IReadOnlyList<string> SubjectNames);

public sealed record InstructorDetail(
    Guid Id,
    Guid MembershipId,
    string FirstName,
    string LastName,
    string? Headline,
    string? Bio,
    string? Hobbies,
    string TimeZoneId,
    InstructorStatus Status,
    decimal? DefaultHourlyRate,
    string? DefaultHourlyRateCurrency,
    IReadOnlyList<InstructorSubjectDetail> Subjects,
    IReadOnlyList<InstructorAvailabilityDetail> Availabilities);

/// <param name="LevelCode">Bos ise egitmen o alanin tum seviyelerinde yetkin.</param>
public sealed record InstructorSubjectDetail(
    Guid Id,
    Guid SubjectId,
    string SubjectName,
    Guid? LevelId,
    string? LevelCode,
    InstructorSubjectStatus Status);

public sealed record InstructorAvailabilityDetail(
    Guid Id,
    DayOfWeek DayOfWeek,
    TimeOnly StartLocalTime,
    TimeOnly EndLocalTime,
    string TimeZoneId,
    DateOnly ValidFrom,
    DateOnly? ValidUntil);

public sealed record AvailabilityOverrideDetail(
    Guid Id,
    DateOnly OverrideDate,
    TimeOnly? StartLocalTime,
    TimeOnly? EndLocalTime,
    AvailabilityOverrideType OverrideType,
    string? Reason);

public sealed record InstructorStudentListItem(
    Guid UserId,
    string FirstName,
    string LastName,
    IReadOnlyList<string> CourseTitles,
    int TotalLessons,
    DateTimeOffset LastLessonAt);

public sealed record InstructorScheduleLearner(
    Guid UserId,
    string FirstName,
    string LastName);

/// <param name="InstructorCancellationDeadlineAt">
/// Bu ana kadar yapilan iptalde egitmene kesinti uygulanmaz.
/// </param>
/// <param name="CanCancel">
/// Ders henuz baslamadiysa dogru. Dort saat siniri iptali kapatmaz, yalnizca
/// kesinti uygulanip uygulanmayacagini belirler.
/// </param>
/// <param name="WillReceivePenaltyIfCancelled">
/// Su anda iptal edilirse kesinti dogar mi.
/// </param>
/// <param name="NextPenaltyPercentage">
/// Kesinti dogarsa uygulanacak oran. Arayuz yuzdeyi tahmin etmez.
/// </param>
public sealed record InstructorScheduleListItem(
    Guid SessionId,
    string CourseTitle,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    LessonSessionStatus Status,
    DateTimeOffset? InstructorCancellationDeadlineAt,
    bool CanCancel,
    bool WillReceivePenaltyIfCancelled,
    decimal NextPenaltyPercentage,
    string? MeetingProvider,
    string? MeetingReference,
    IReadOnlyList<InstructorScheduleLearner> Learners);

public sealed record InstructorDashboardStats(
    int StudentCount,
    int CompletedLessons,
    decimal ThisMonthTotal,
    string Currency,
    double? AverageRating);

public sealed record InstructorEarningListItem(
    Guid SessionId,
    string CourseTitle,
    DateTimeOffset StartsAt,
    int LearnerCount,
    decimal Amount,
    string Currency,
    decimal GrossAmount,
    decimal PenaltyPercentage,
    decimal PenaltyAmount);
