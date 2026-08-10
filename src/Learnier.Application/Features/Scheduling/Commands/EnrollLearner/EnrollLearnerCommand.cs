using FluentValidation;
using Learnier.Application.Common.Abstractions;
using Learnier.Application.Common.Results;

namespace Learnier.Application.Features.Scheduling.Commands.EnrollLearner;

public sealed record EnrollLearnerCommand(Guid ClassGroupId, Guid LearnerUserId);

public sealed record EnrollLearnerResult(Guid MemberId);

internal sealed class EnrollLearnerValidator : AbstractValidator<EnrollLearnerCommand>
{
    public EnrollLearnerValidator()
    {
        RuleFor(c => c.LearnerUserId)
            .NotEmpty().WithErrorCode("scheduling.learner_required");
    }
}

/// <summary>
/// Ogrenciyi sinifa kaydeder.
/// </summary>
/// <remarks>
/// Kapasite kontrolu burada veritabanindan sayilarak yapiliyor, bellekteki
/// koleksiyondan degil. Yine de rezervasyondaki kadar siki degil: sinifa kayit
/// yoneticinin denetiminde ve seyrek yapilan bir islem, bu yuzden satir kilidi
/// maliyeti tercih edilmedi. Kontenjan yarisi asil rezervasyon akisinda kritik.
/// </remarks>
public sealed class EnrollLearnerHandler(
    ISchedulingRepository scheduling,
    IUserRepository users,
    ICurrentTenant currentTenant,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    public async Task<Result<EnrollLearnerResult>> Handle(
        EnrollLearnerCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!currentTenant.HasTenant)
        {
            return SchedulingErrors.OrganizationContextRequired;
        }

        var classGroup = await scheduling.FindClassGroupAsync(
            command.ClassGroupId, includeMembers: true, cancellationToken);

        if (classGroup is null)
        {
            return SchedulingErrors.ClassGroupNotFound;
        }

        var learner = await users.FindByIdAsync(command.LearnerUserId, cancellationToken);

        if (learner is null)
        {
            return SchedulingErrors.LearnerNotFound;
        }

        var activeCount = await scheduling.CountActiveMembersAsync(classGroup.Id, cancellationToken);

        // Zaten kayitliysa tekrar kontenjan harcamaz; Enroll mevcut kaydi dondurur.
        var alreadyEnrolled = classGroup.Members.Any(m => m.LearnerUserId == learner.Id);

        if (!alreadyEnrolled && activeCount >= classGroup.Capacity)
        {
            return SchedulingErrors.ClassGroupFull(classGroup.Capacity);
        }

        var member = classGroup.Enroll(learner.Id, clock.UtcNow);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new EnrollLearnerResult(member.Id);
    }
}
