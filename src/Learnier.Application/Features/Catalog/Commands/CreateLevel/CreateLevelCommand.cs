using FluentValidation;
using Learnier.Application.Common.Abstractions;
using Learnier.Application.Common.Results;
using Learnier.Domain.Catalog;

namespace Learnier.Application.Features.Catalog.Commands.CreateLevel;

/// <param name="SortOrder">Seviyelerin dogal sirasi; karsilastirma bu alanla yapilir.</param>
public sealed record CreateLevelCommand(Guid SubjectId, string Code, string Name, int SortOrder);

public sealed record CreateLevelResult(Guid LevelId, string Code);

internal sealed class CreateLevelValidator : AbstractValidator<CreateLevelCommand>
{
    public CreateLevelValidator()
    {
        RuleFor(c => c.SubjectId)
            .NotEmpty().WithErrorCode("catalog.subject_required");

        RuleFor(c => c.Code)
            .NotEmpty().WithErrorCode("catalog.level_code_required")
            .MaximumLength(32).WithErrorCode("catalog.level_code_too_long");

        RuleFor(c => c.Name)
            .NotEmpty().WithErrorCode("catalog.level_name_required")
            .MaximumLength(128).WithErrorCode("catalog.level_name_too_long");
    }
}

/// <summary>
/// Bir egitim alanina seviye ekler.
/// </summary>
/// <remarks>
/// Seviyeler alana baglidir; boylece Ingilizcede A1-C2, matematikte "5. sinif"
/// ayni tabloda yasayabilir.
/// </remarks>
public sealed class CreateLevelHandler(
    ICatalogRepository catalog,
    ICurrentTenant currentTenant,
    IUnitOfWork unitOfWork)
{
    public async Task<Result<CreateLevelResult>> Handle(
        CreateLevelCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!currentTenant.HasTenant)
        {
            return CatalogErrors.OrganizationContextRequired;
        }

        // Kiraci filtresi geregi baska kurumun alanina seviye eklenemez.
        var subject = await catalog.FindSubjectAsync(command.SubjectId, cancellationToken);

        if (subject is null)
        {
            return CatalogErrors.SubjectNotFound;
        }

        var code = command.Code.Trim();

        if (await catalog.LevelCodeExistsAsync(subject.Id, code, cancellationToken))
        {
            return CatalogErrors.LevelCodeAlreadyTaken(code);
        }

        var level = Level.Create(subject.Id, code, command.Name, command.SortOrder);

        catalog.AddLevel(level);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new CreateLevelResult(level.Id, level.Code);
    }
}
