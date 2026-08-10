using FluentValidation;

namespace Learnier.Application.Features.Catalog.Commands.CreateSubject;

/// <param name="ParentSubjectId">Ust alan; "Yazilim > Backend" gibi tek seviyeli kirilim icin.</param>
public sealed record CreateSubjectCommand(string Name, string Slug, Guid? ParentSubjectId);

public sealed record CreateSubjectResult(Guid SubjectId, string Slug);

internal sealed class CreateSubjectValidator : AbstractValidator<CreateSubjectCommand>
{
    public CreateSubjectValidator()
    {
        RuleFor(c => c.Name)
            .NotEmpty().WithErrorCode("catalog.subject_name_required")
            .MaximumLength(200).WithErrorCode("catalog.subject_name_too_long");

        RuleFor(c => c.Slug)
            .NotEmpty().WithErrorCode("catalog.subject_slug_required")
            .MaximumLength(100).WithErrorCode("catalog.subject_slug_too_long")
            .Matches("^[a-z0-9]+(-[a-z0-9]+)*$").WithErrorCode("catalog.subject_slug_invalid");
    }
}
