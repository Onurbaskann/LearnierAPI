using Learnier.Application.Common.Abstractions;
using Learnier.Application.Common.Results;

namespace Learnier.Application.Features.Teaching.Commands.SuspendInstructor;

/// <summary>Egitmen profilini yonetici karariyla askiya alir.</summary>
public sealed class SuspendInstructorHandler(
    IInstructorRepository instructors,
    ICurrentTenant currentTenant,
    IUnitOfWork unitOfWork)
{
    public async Task<Result> Handle(Guid profileId, CancellationToken cancellationToken)
    {
        if (!currentTenant.HasTenant)
        {
            return Result.Failure(TeachingErrors.OrganizationContextRequired);
        }

        var profile = await instructors.FindWithDetailsAsync(profileId, cancellationToken);

        if (profile is null)
        {
            return Result.Failure(TeachingErrors.ProfileNotFound);
        }

        profile.Suspend();
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
