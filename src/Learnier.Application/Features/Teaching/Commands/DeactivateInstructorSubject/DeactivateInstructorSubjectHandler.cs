using Learnier.Application.Common.Abstractions;
using Learnier.Application.Common.Results;

namespace Learnier.Application.Features.Teaching.Commands.DeactivateInstructorSubject;

/// <summary>Egitmenin bir brans yetkinligini gecmis kaydi silmeden pasiflestirir.</summary>
public sealed class DeactivateInstructorSubjectHandler(
    IInstructorRepository instructors,
    ICurrentTenant currentTenant,
    IUnitOfWork unitOfWork)
{
    public async Task<Result> Handle(
        Guid profileId,
        Guid instructorSubjectId,
        bool canManageInstructors,
        CancellationToken cancellationToken)
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

        if (InstructorAccess.Check(profile, currentTenant, canManageInstructors) is { } denied)
        {
            return Result.Failure(denied);
        }

        var instructorSubject = profile.Subjects.SingleOrDefault(s => s.Id == instructorSubjectId);

        if (instructorSubject is null)
        {
            return Result.Failure(TeachingErrors.InstructorSubjectNotFound);
        }

        instructorSubject.Deactivate();
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
