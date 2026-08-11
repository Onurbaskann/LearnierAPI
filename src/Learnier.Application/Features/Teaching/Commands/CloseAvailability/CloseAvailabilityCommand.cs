using Learnier.Application.Common.Abstractions;
using Learnier.Application.Common.Results;

namespace Learnier.Application.Features.Teaching.Commands.CloseAvailability;

public sealed record CloseAvailabilityCommand(
    Guid ProfileId,
    Guid AvailabilityId,
    DateOnly ValidUntil);

/// <summary>Haftalik uygunlugu belirtilen tarihte kapatir; gecmis kayit korunur.</summary>
public sealed class CloseAvailabilityHandler(
    IInstructorRepository instructors,
    ICurrentTenant currentTenant,
    IUnitOfWork unitOfWork)
{
    public async Task<Result> Handle(
        CloseAvailabilityCommand command,
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

        var availability = profile.Availabilities.SingleOrDefault(a => a.Id == command.AvailabilityId);

        if (availability is null)
        {
            return Result.Failure(TeachingErrors.AvailabilityNotFound);
        }

        if (command.ValidUntil < availability.ValidFrom)
        {
            return Result.Failure(TeachingErrors.AvailabilityDateRangeInvalid);
        }

        availability.Close(command.ValidUntil);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
