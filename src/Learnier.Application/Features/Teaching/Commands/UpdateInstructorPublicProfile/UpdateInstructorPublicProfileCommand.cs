using FluentValidation;
using Learnier.Application.Common.Abstractions;
using Learnier.Application.Common.Results;

namespace Learnier.Application.Features.Teaching.Commands.UpdateInstructorPublicProfile;

public sealed record UpdateInstructorPublicProfileCommand(
    Guid ProfileId,
    string? Headline,
    string? Bio,
    string? Hobbies);

internal sealed class UpdateInstructorPublicProfileValidator
    : AbstractValidator<UpdateInstructorPublicProfileCommand>
{
    public UpdateInstructorPublicProfileValidator()
    {
        RuleFor(c => c.ProfileId)
            .NotEmpty().WithErrorCode("teaching.profile_required");

        RuleFor(c => c.Headline)
            .MaximumLength(160).WithErrorCode("teaching.headline_too_long");

        RuleFor(c => c.Bio)
            .MaximumLength(4000).WithErrorCode("teaching.bio_too_long");

        RuleFor(c => c.Hobbies)
            .MaximumLength(500).WithErrorCode("teaching.hobbies_too_long");
    }
}

public sealed class UpdateInstructorPublicProfileHandler(
    IInstructorRepository instructors,
    ICurrentTenant currentTenant,
    IUnitOfWork unitOfWork)
{
    public async Task<Result> Handle(
        UpdateInstructorPublicProfileCommand command,
        bool canManageInstructors,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!currentTenant.HasTenant)
        {
            return Result.Failure(TeachingErrors.OrganizationContextRequired);
        }

        var profile = await instructors.FindWithDetailsAsync(command.ProfileId, cancellationToken);
        if (profile is null)
        {
            return Result.Failure(TeachingErrors.ProfileNotFound);
        }

        if (InstructorAccess.Check(profile, currentTenant, canManageInstructors) is { } denied)
        {
            return Result.Failure(denied);
        }

        profile.UpdatePublicProfile(command.Headline, command.Bio, command.Hobbies);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
