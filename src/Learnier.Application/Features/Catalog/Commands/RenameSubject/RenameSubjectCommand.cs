using FluentValidation;

namespace Learnier.Application.Features.Catalog.Commands.RenameSubject;

public sealed record RenameSubjectCommand(Guid SubjectId, string Name);

internal sealed class RenameSubjectValidator : AbstractValidator<RenameSubjectCommand>
{
    public RenameSubjectValidator()
    {
        RuleFor(c => c.SubjectId)
            .NotEmpty().WithErrorCode("catalog.subject_required");

        RuleFor(c => c.Name)
            .NotEmpty().WithErrorCode("catalog.subject_name_required")
            .MaximumLength(200).WithErrorCode("catalog.subject_name_too_long");
    }
}
