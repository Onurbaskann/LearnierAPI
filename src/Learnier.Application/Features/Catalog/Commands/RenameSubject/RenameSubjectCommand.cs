using FluentValidation;

namespace Learnier.Application.Features.Catalog.Commands.RenameSubject;

/// <remarks>
/// Alan kimligi komutta degil handler parametresinde tasinir: rotadan geliyor.
/// Komut yalnizca govdeden geleni tutarsa action parametresi olarak baglanabilir
/// ve <c>ValidationFilter</c> kurallari calistirabilir.
/// </remarks>
public sealed record RenameSubjectCommand(string Name);

internal sealed class RenameSubjectValidator : AbstractValidator<RenameSubjectCommand>
{
    public RenameSubjectValidator()
    {
        RuleFor(c => c.Name)
            .NotEmpty().WithErrorCode("catalog.subject_name_required")
            .MaximumLength(200).WithErrorCode("catalog.subject_name_too_long");
    }
}
