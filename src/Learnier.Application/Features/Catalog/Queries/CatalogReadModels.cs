using Learnier.Application.Common.Models;
using Learnier.Domain.Catalog;

namespace Learnier.Application.Features.Catalog.Queries;

/// <param name="ParentSubjectId">Ust alan; kok alanlarda bos.</param>
public sealed record SubjectListItem(
    Guid Id,
    string Name,
    string Slug,
    Guid? ParentSubjectId,
    SubjectStatus Status,
    int CourseCount);

public sealed record LevelListItem(Guid Id, string Code, string Name, int SortOrder);

public sealed record CourseListItem(
    Guid Id,
    string Title,
    Guid SubjectId,
    string SubjectName,
    string? LevelCode,
    CourseType CourseType,
    CourseStatus Status,
    int DefaultDurationMinutes,
    int MaxParticipants);

/// <summary>
/// Egitim listesinin filtreleri.
/// </summary>
/// <param name="IncludeUnpublished">
/// Taslak ve arsivlenmis egitimleri de kapsar. Yalnizca katalog yonetme izni
/// olan cagirici icin dogru verilir; aksi halde hazir olmayan icerik gorunurdu.
/// </param>
public sealed record CourseListFilter(
    PageRequest Page,
    Guid? SubjectId = null,
    Guid? LevelId = null,
    CourseType? CourseType = null,
    bool IncludeUnpublished = false);

public sealed record CourseDetail(
    Guid Id,
    string Title,
    string? Description,
    Guid SubjectId,
    string SubjectName,
    string? LevelCode,
    CourseType CourseType,
    CourseStatus Status,
    int DefaultDurationMinutes,
    int MinParticipants,
    int MaxParticipants,
    IReadOnlyList<CourseModuleDetail> Modules);

public sealed record CourseModuleDetail(
    Guid Id,
    string Title,
    string? Description,
    int SortOrder,
    IReadOnlyList<CourseLessonDetail> Lessons);

public sealed record CourseLessonDetail(
    Guid Id,
    string Title,
    string? Description,
    int SortOrder,
    int EstimatedDurationMinutes);
