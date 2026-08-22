using FluentValidation;
using Learnier.Application.Common.Abstractions;
using Learnier.Application.Common.Results;

namespace Learnier.Application.Features.Catalog.Commands.AddCourseLesson;

/// <remarks>
/// Modul kimligi komutta degil handler parametresinde tasinir: rotadan geliyor.
/// Komut yalnizca govdeden geleni tutarsa action parametresi olarak baglanabilir
/// ve <c>ValidationFilter</c> kurallari calistirabilir.
/// </remarks>
public sealed record AddCourseLessonCommand(
    string Title,
    int SortOrder,
    int EstimatedDurationMinutes,
    string? Description = null);

public sealed record AddCourseLessonResult(Guid LessonId);

internal sealed class AddCourseLessonValidator : AbstractValidator<AddCourseLessonCommand>
{
    public AddCourseLessonValidator()
    {
        RuleFor(c => c.Title)
            .NotEmpty().WithErrorCode("catalog.lesson_title_required")
            .MaximumLength(200).WithErrorCode("catalog.lesson_title_too_long");

        RuleFor(c => c.SortOrder)
            .GreaterThanOrEqualTo(0).WithErrorCode("catalog.sort_order_invalid");

        RuleFor(c => c.EstimatedDurationMinutes)
            .InclusiveBetween(1, 600).WithErrorCode("catalog.lesson_duration_invalid");

        RuleFor(c => c.Description)
            .MaximumLength(2000).WithErrorCode("catalog.lesson_description_too_long");
    }
}

/// <summary>
/// Module mufredat dersi ekler.
/// </summary>
/// <remarks>
/// Buradaki "ders" mufredattaki bir baslik; takvimdeki oturum degil. Kaynak
/// dokumanin 1. bolumundeki ayrim bu: gerceklesen ders <c>LessonSession</c>.
/// </remarks>
public sealed class AddCourseLessonHandler(
    ICatalogRepository catalog,
    ICurrentTenant currentTenant,
    IUnitOfWork unitOfWork)
{
    public async Task<Result<AddCourseLessonResult>> Handle(
        Guid moduleId,
        AddCourseLessonCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!currentTenant.HasTenant)
        {
            return CatalogErrors.OrganizationContextRequired;
        }

        // Modul kendi kiraci sutununu tasimadigi icin sorgu egitim uzerinden
        // dogrulanir; baska kurumun modulu bulunamaz.
        var module = await catalog.FindModuleWithCourseAsync(moduleId, cancellationToken);

        if (module is null)
        {
            return CatalogErrors.ModuleNotFound;
        }

        var lesson = module.AddLesson(
            command.Title,
            command.SortOrder,
            command.EstimatedDurationMinutes,
            command.Description);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new AddCourseLessonResult(lesson.Id);
    }
}
