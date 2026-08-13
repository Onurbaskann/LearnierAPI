using FluentValidation;
using Learnier.Application.Common.Abstractions;
using Learnier.Application.Common.Results;
using Learnier.Domain.Billing;
using Learnier.Domain.Scheduling;

namespace Learnier.Application.Features.Subscriptions;

public sealed record PurchaseDemoPackageCommand(
    Guid SubjectId,
    int LessonsPerWeek,
    int DurationMonths);

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
    private const decimal BasePricePerLesson = 250m;

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
        var totalCredits = command.LessonsPerWeek * WeeksPerMonth * command.DurationMonths;
        var planName = $"{subject.Name} {command.LessonsPerWeek}x{command.DurationMonths} Demo";
        var plan = await repository.FindPlanAsync(organizationId, planName, cancellationToken);
        PlanPrice price;

        if (plan is null)
        {
            plan = SubscriptionPlan.Create(
                organizationId,
                planName,
                CatalogAccess.Restricted,
                "Ödeme sağlayıcısı bağlanana kadar kullanılan kalıcı demo paketi.");
            plan.AddEntitlement(
                EntitlementType.LessonCredit,
                SessionType.Private,
                totalCredits,
                EntitlementResetPeriod.Subscription);
            price = plan.AddPrice(
                "TRY",
                CalculatePrice(command.LessonsPerWeek, command.DurationMonths, totalCredits),
                BillingInterval.Month,
                command.DurationMonths,
                now);
            plan.Activate();
            repository.AddPlan(plan);
            repository.AddSubjectAccess(PlanSubjectAccess.Create(plan.Id, subject.Id));
        }
        else
        {
            price = plan.Prices.FirstOrDefault(item => item.Status == PlanPriceStatus.Active)
                ?? plan.AddPrice(
                    "TRY",
                    CalculatePrice(command.LessonsPerWeek, command.DurationMonths, totalCredits),
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
            totalCredits,
            now,
            periodEnd));

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new PurchaseDemoPackageResult(
            subscription.Id,
            subject.Id,
            subject.Name,
            totalCredits,
            periodEnd);
    }

    private static decimal CalculatePrice(int lessonsPerWeek, int durationMonths, int totalCredits)
    {
        var frequencyDiscount = lessonsPerWeek switch
        {
            3 => 0.05m,
            5 => 0.12m,
            _ => 0m
        };
        var durationDiscount = durationMonths == 12 ? 0.10m : 0m;
        return decimal.Round(
            totalCredits * BasePricePerLesson * (1m - frequencyDiscount - durationDiscount),
            0,
            MidpointRounding.AwayFromZero);
    }
}
