using FluentValidation;
using Learnier.Application.Common.Abstractions;
using Learnier.Application.Common.Results;

namespace Learnier.Application.Features.Catalog.Commands.AddCourseModule;

/// <remarks>
/// Egitim kimligi komutta degil handler parametresinde tasinir: rotadan geliyor.
/// Komut yalnizca govdeden geleni tutarsa action parametresi olarak baglanabilir
/// ve <c>ValidationFilter</c> kurallari calistirabilir.
/// </remarks>
public sealed record AddCourseModuleCommand(
    string Title,
    int SortOrder,
    string? Description = null);

public sealed record AddCourseModuleResult(Guid ModuleId);

internal sealed class AddCourseModuleValidator : AbstractValidator<AddCourseModuleCommand>
{
    public AddCourseModuleValidator()
    {
        RuleFor(c => c.Title)
            .NotEmpty().WithErrorCode("catalog.module_title_required")
            .MaximumLength(200).WithErrorCode("catalog.module_title_too_long");

        RuleFor(c => c.SortOrder)
            .GreaterThanOrEqualTo(0).WithErrorCode("catalog.sort_order_invalid");

        RuleFor(c => c.Description)
            .MaximumLength(2000).WithErrorCode("catalog.module_description_too_long");
    }
}

/// <summary>
/// Egitime mufredat modulu ekler.
/// </summary>
/// <remarks>
/// Modul kendi <c>OrganizationId</c> sutununu tasimaz (kaynak dokuman 12. bolum);
/// erisim sinirinin korunmasi, egitimin kiraci filtresine tabi sorguyla
/// bulunmasina dayanir.
/// </remarks>
public sealed class AddCourseModuleHandler(
    ICatalogRepository catalog,
    ICurrentTenant currentTenant,
    IUnitOfWork unitOfWork)
{
    public async Task<Result<AddCourseModuleResult>> Handle(
        Guid courseId,
        AddCourseModuleCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!currentTenant.HasTenant)
        {
            return CatalogErrors.OrganizationContextRequired;
        }

        var course = await catalog.FindCourseAsync(courseId, includeModules: true, cancellationToken);

        if (course is null)
        {
            return CatalogErrors.CourseNotFound;
        }

        var module = course.AddModule(command.Title, command.SortOrder, command.Description);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new AddCourseModuleResult(module.Id);
    }
}
