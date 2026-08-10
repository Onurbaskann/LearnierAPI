using Learnier.Application.Common.Abstractions;
using Learnier.Application.Common.Results;

namespace Learnier.Application.Features.Teaching.Commands.ActivateInstructor;

/// <summary>
/// Egitmen profilini aktiflestirir.
/// </summary>
/// <remarks>
/// Yalnizca egitmenleri yonetme yetkisi olanlar cagirabilir; egitmen kendi
/// basvurusunu onaylayamamali.
/// </remarks>
public sealed class ActivateInstructorHandler(
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

        profile.Activate();
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
