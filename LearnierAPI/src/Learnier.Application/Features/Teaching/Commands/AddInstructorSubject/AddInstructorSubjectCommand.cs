using FluentValidation;
using Learnier.Application.Common.Abstractions;
using Learnier.Application.Common.Results;

namespace Learnier.Application.Features.Teaching.Commands.AddInstructorSubject;

/// <param name="LevelId">
/// Bos birakilirsa egitmen o alanin tum seviyelerinde yetkin sayilir.
/// </param>
public sealed record AddInstructorSubjectCommand(Guid SubjectId, Guid? LevelId);

public sealed record AddInstructorSubjectResult(Guid InstructorSubjectId);

internal sealed class AddInstructorSubjectValidator : AbstractValidator<AddInstructorSubjectCommand>
{
    public AddInstructorSubjectValidator()
    {
        RuleFor(c => c.SubjectId)
            .NotEmpty().WithErrorCode("catalog.subject_required");
    }
}

/// <summary>
/// Egitmene brans yetkinligi ekler.
/// </summary>
public sealed class AddInstructorSubjectHandler(
    IInstructorRepository instructors,
    ICatalogRepository catalog,
    ICurrentTenant currentTenant,
    IUnitOfWork unitOfWork)
{
    public async Task<Result<AddInstructorSubjectResult>> Handle(
        Guid profileId,
        AddInstructorSubjectCommand command,
        bool canManageInstructors,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!currentTenant.HasTenant)
        {
            return TeachingErrors.OrganizationContextRequired;
        }

        var profile = await instructors.FindWithDetailsAsync(profileId, cancellationToken);

        if (profile is null)
        {
            return TeachingErrors.ProfileNotFound;
        }

        if (InstructorAccess.Check(profile, currentTenant, canManageInstructors) is { } denied)
        {
            return denied;
        }

        // Alan sorgusu kiraci filtresine tabi: baska kurumun alani secilemez.
        var subject = await catalog.FindSubjectAsync(command.SubjectId, cancellationToken);

        if (subject is null)
        {
            return TeachingErrors.SubjectNotFound;
        }

        if (command.LevelId is { } levelId)
        {
            var level = await catalog.FindLevelAsync(levelId, cancellationToken);

            if (level is null)
            {
                return TeachingErrors.LevelNotFound;
            }

            // Seviye secilen alana ait olmali; aksi halde "Ingilizce A1 yetkinligi
            // olan matematik egitmeni" gibi anlamsiz kayitlar olusurdu.
            if (level.SubjectId != subject.Id)
            {
                return TeachingErrors.LevelSubjectMismatch;
            }
        }

        // Ayni alan/seviye ikilisi iki kez eklenmez; mevcut kayit doner.
        var instructorSubject = profile.AddSubject(subject.Id, command.LevelId);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new AddInstructorSubjectResult(instructorSubject.Id);
    }
}
