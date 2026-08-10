using Learnier.Domain.Teaching;

namespace Learnier.Application.Features.Teaching.Queries;

public sealed record InstructorListItem(
    Guid Id,
    Guid MembershipId,
    string FirstName,
    string LastName,
    InstructorStatus Status,
    string TimeZoneId,
    IReadOnlyList<string> SubjectNames);

public sealed record InstructorDetail(
    Guid Id,
    Guid MembershipId,
    string FirstName,
    string LastName,
    string? Bio,
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
