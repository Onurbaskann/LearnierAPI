using FluentValidation;
using Learnier.Application.Common.Abstractions;
using Learnier.Application.Common.Results;
using Learnier.Domain.Billing;
using Learnier.Domain.Scheduling;

namespace Learnier.Application.Features.Subscriptions;

public sealed record PurchaseDemoPackageCommand(
    Guid SubjectId,
    int LessonsPerWeek,
    int DurationMonths,
    int LessonDurationMinutes = 50);

public sealed record PurchaseDemoPackageResult(
    Guid SubscriptionId,
    Guid SubjectId,
    string SubjectName,
    int GrantedCredits,
    DateTimeOffset CurrentPeriodEnd);

internal sealed class PurchaseDemoPackageValidator
    : AbstractValidator<PurchaseDemoPackageCommand>
{
    private static readonly int[] AllowedFrequencies = [2, 3, 5];
    private static readonly int[] AllowedDurations = [6, 12];

    public PurchaseDemoPackageValidator()
    {
        RuleFor(command => command.SubjectId)
            .NotEmpty().WithErrorCode("subscriptions.subject_required");
        RuleFor(command => command.LessonsPerWeek)
            .Must(AllowedFrequencies.Contains).WithErrorCode("subscriptions.frequency_invalid");
        RuleFor(command => command.DurationMonths)
            .Must(AllowedDurations.Contains).WithErrorCode("subscriptions.duration_invalid");
        RuleFor(command => command.LessonDurationMinutes)
            .Must(duration => duration is 30 or 50)
            .WithErrorCode("subscriptions.lesson_duration_invalid");
    }
}

public sealed class PurchaseDemoPackageHandler(
    IPackagePurchaseRepository repository,
    IActivePackageQueries activePackages,
    ICurrentUser currentUser,
    ICurrentTenant currentTenant,
    IClock clock,
    IUnitOfWork unitOfWork)
{
    private const int WeeksPerMonth = 4;
    public async Task<Result<PurchaseDemoPackageResult>> Handle(
        PurchaseDemoPackageCommand command,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId
            || currentTenant.OrganizationId is not { } organizationId)
        {
            return Error.Unauthorized("common.unauthorized");
        }

        var subject = await repository.FindSubjectAsync(command.SubjectId, cancellationToken);
        if (subject is null)
        {
            return Error.NotFound("subscriptions.subject_not_found");
        }

        var existing = (await activePackages.ListForUserAsync(userId, cancellationToken))
            .FirstOrDefault(package => package.SubjectId == subject.Id);
        if (existing is not null)
        {
            return new PurchaseDemoPackageResult(
                existing.SubscriptionId,
                existing.SubjectId,
                existing.SubjectName,
                existing.RemainingCredits,
                existing.CurrentPeriodEnd);
        }

        var now = clock.UtcNow;
        var monthlyCredits = command.LessonsPerWeek * WeeksPerMonth;
        var totalCredits = monthlyCredits * command.DurationMonths;
        var planName = $"{subject.Name} {command.LessonsPerWeek}x{command.DurationMonths} "
            + $"{command.LessonDurationMinutes}dk Demo";
        var plan = await repository.FindPlanAsync(organizationId, planName, cancellationToken);
        PlanPrice price;

        if (plan is null)
        {
            plan = SubscriptionPlan.CreateLessonPackage(
                organizationId,
                planName,
                monthlyCredits,
                command.LessonDurationMinutes,
                "Ödeme sağlayıcısı bağlanana kadar kullanılan kalıcı demo paketi.");
            price = plan.AddPrice(
                "TRY",
                LessonPackagePricing.CalculateTotal(
                    command.LessonsPerWeek,
                    command.DurationMonths,
                    command.LessonDurationMinutes),
                BillingInterval.Month,
                command.DurationMonths,
                now);
            plan.Activate();
            repository.AddPlan(plan);
            repository.AddSubjectAccess(PlanSubjectAccess.Create(plan.Id, subject.Id));
        }
        else
        {
            plan.ConfigureLessonPackage(monthlyCredits, command.LessonDurationMinutes);
            price = plan.Prices.FirstOrDefault(item => item.Status == PlanPriceStatus.Active)
                ?? plan.AddPrice(
                    "TRY",
                    LessonPackagePricing.CalculateTotal(
                        command.LessonsPerWeek,
                        command.DurationMonths,
                        command.LessonDurationMinutes),
                    BillingInterval.Month,
                    command.DurationMonths,
                    now);
        }

        var periodEnd = now.AddMonths(command.DurationMonths);
        var subscription = Subscription.CreateForUser(
            organizationId,
            userId,
            price.Id,
            now,
            periodEnd);
        subscription.Activate();

        repository.AddSubscription(subscription);
        repository.AddCredit(CreditLedgerEntry.Grant(
            subscription.Id,
            userId,
            SessionType.Private,
            monthlyCredits,
            now,
            now.AddMonths(1),
            now));

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new PurchaseDemoPackageResult(
            subscription.Id,
            subject.Id,
            subject.Name,
            monthlyCredits,
            periodEnd);
    }

}
