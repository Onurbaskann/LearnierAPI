using Learnier.Application.Common.Abstractions;
using Learnier.Application.Common.Results;
using Learnier.Domain.Billing;

namespace Learnier.Application.Features.Billing.Commands.GrantPlanAccess;

/// <param name="SubjectId">Verilirse alanin tamami plana dahil edilir.</param>
/// <param name="CourseId">Verilirse yalnizca o egitim dahil edilir.</param>
public sealed record GrantPlanAccessCommand(Guid PlanId, Guid? SubjectId, Guid? CourseId);

/// <summary>
/// Kisitli kapsamli plana alan veya egitim erisimi ekler.
/// </summary>
/// <remarks>
/// <para>
/// Kaynak dokumanin 8. bolumu, soyut bir <c>target_type/target_id</c> tablosunu
/// acikca onermiyor: yabanci anahtar butunlugunu bozar ve sorgulari zorlastirir.
/// Bu yuzden alan ve egitim erisimi ayri tablolarda tutuluyor.
/// </para>
/// <para>
/// Hedef kontrolu FluentValidation ile degil burada yapiliyor: plan kimligi
/// rotadan geldigi icin komut action icinde kuruluyor ve
/// <c>ValidationFilter</c> yalnizca action parametrelerini gorebiliyor.
/// </para>
/// </remarks>
public sealed class GrantPlanAccessHandler(
    IPlanRepository plans,
    ICatalogRepository catalog,
    ICurrentTenant currentTenant,
    IUnitOfWork unitOfWork)
{
    public async Task<Result> Handle(
        GrantPlanAccessCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!currentTenant.HasTenant)
        {
            return Result.Failure(BillingErrors.OrganizationContextRequired);
        }

        // Tam olarak biri verilmeli: ikisi birden anlamsiz, hicbiri eksik.
        if (command.SubjectId is not null == (command.CourseId is not null))
        {
            return Result.Failure(BillingErrors.AccessTargetInvalid);
        }

        var plan = await plans.FindPlanAsync(command.PlanId, includeDetails: false, cancellationToken);

        if (plan is null)
        {
            return Result.Failure(BillingErrors.PlanNotFound);
        }

        if (command.SubjectId is { } subjectId)
        {
            // Kiraci filtresi geregi baska kurumun alani plana eklenemez.
            var subject = await catalog.FindSubjectAsync(subjectId, cancellationToken);

            if (subject is null)
            {
                return Result.Failure(BillingErrors.SubjectNotFound);
            }

            plans.AddSubjectAccess(PlanSubjectAccess.Create(plan.Id, subject.Id));
        }
        else if (command.CourseId is { } courseId)
        {
            var course = await catalog.FindCourseAsync(courseId, includeModules: false, cancellationToken);

            if (course is null)
            {
                return Result.Failure(BillingErrors.CourseNotFound);
            }

            plans.AddCourseAccess(PlanCourseAccess.Create(plan.Id, course.Id));
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
