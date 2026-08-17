using FluentValidation;

namespace Learnier.Application.Features.Clubs.Commands.CreateClub;

public sealed record CreateClubCommand(
    Guid SubjectId,
    string Name,
    string? Description);

public sealed record CreateClubResult(Guid ClubId);

internal sealed class CreateClubValidator : AbstractValidator<CreateClubCommand>
{
    public CreateClubValidator()
    {
        RuleFor(command => command.SubjectId)
            .NotEmpty().WithErrorCode("clubs.subject_id_required");

        RuleFor(command => command.Name)
            .NotEmpty().WithErrorCode("clubs.name_required")
            .MaximumLength(200).WithErrorCode("clubs.name_too_long");

        RuleFor(command => command.Description)
            .MaximumLength(1000).WithErrorCode("clubs.description_too_long");
    }
}
