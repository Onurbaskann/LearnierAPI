using FluentValidation;
using Learnier.Application.Common.Abstractions;
using Learnier.Application.Common.Results;
using Learnier.Domain.Catalog;

namespace Learnier.Application.Features.Catalog.Commands.CreateCourse;

public sealed record CreateCourseCommand(
    Guid SubjectId,
    string Title,
    CourseType CourseType,
    int DefaultDurationMinutes,
    int MinParticipants,
    int MaxParticipants,
    Guid? LevelId = null,
    string? Description = null);

public sealed record CreateCourseResult(Guid CourseId, CourseStatus Status);

internal sealed class CreateCourseValidator : AbstractValidator<CreateCourseCommand>
{
    public CreateCourseValidator()
    {
        RuleFor(c => c.SubjectId)
            .NotEmpty().WithErrorCode("catalog.subject_required");

        RuleFor(c => c.Title)
            .NotEmpty().WithErrorCode("catalog.course_title_required")
            .MaximumLength(200).WithErrorCode("catalog.course_title_too_long");

        RuleFor(c => c.CourseType)
            .IsInEnum().WithErrorCode("catalog.course_type_invalid");

        // Ust sinir gunluk bir oturumun makul suresini asmasin diye.
        RuleFor(c => c.DefaultDurationMinutes)
            .InclusiveBetween(5, 600).WithErrorCode("catalog.course_duration_invalid");

        RuleFor(c => c.MinParticipants)
            .GreaterThan(0).WithErrorCode("catalog.course_min_participants_invalid");

        RuleFor(c => c.MaxParticipants)
            .GreaterThanOrEqualTo(c => c.MinParticipants)
            .WithErrorCode("catalog.course_max_participants_invalid");

        RuleFor(c => c.Description)
            .MaximumLength(4000).WithErrorCode("catalog.course_description_too_long");
    }
}

/// <summary>
/// Yeni egitim tanimi olusturur.
/// </summary>
/// <remarks>
/// Egitim taslak olarak baslar ve ayri bir istekle yayina alinir. Bu ayrim bilincli:
/// mufredati tamamlanmamis bir egitim ogrencilere gorunmemeli.
/// </remarks>
public sealed class CreateCourseHandler(
    ICatalogRepository catalog,
    ICurrentTenant currentTenant,
    IUnitOfWork unitOfWork)
{
    public async Task<Result<CreateCourseResult>> Handle(
        CreateCourseCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (currentTenant.OrganizationId is not { } organizationId)
        {
            return CatalogErrors.OrganizationContextRequired;
        }

        var subject = await catalog.FindSubjectAsync(command.SubjectId, cancellationToken);

        if (subject is null)
        {
            return CatalogErrors.SubjectNotFound;
        }

        if (command.LevelId is { } levelId)
        {
            var level = await catalog.FindLevelAsync(levelId, cancellationToken);

            if (level is null)
            {
                return CatalogErrors.LevelNotFound;
            }

            // Seviye, egitimin alanina ait olmali: "A1" seviyesi matematik
            // egitimine atanabilseydi seviye karsilastirmasi anlamsizlasirdi.
            if (level.SubjectId != subject.Id)
            {
                return CatalogErrors.LevelSubjectMismatch;
            }
        }

        var course = Course.Create(
            organizationId,
            subject.Id,
            command.Title,
            command.CourseType,
            command.DefaultDurationMinutes,
            command.MinParticipants,
            command.MaxParticipants,
            command.LevelId,
            command.Description);

        catalog.AddCourse(course);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new CreateCourseResult(course.Id, course.Status);
    }
}
