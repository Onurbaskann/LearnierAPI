using FluentValidation;
using Learnier.Application.Common.Abstractions;
using Learnier.Application.Common.Results;
using Learnier.Domain.Billing;

namespace Learnier.Application.Features.Billing.Commands.CreatePlan;

/// <param name="CatalogAccess">
/// <c>All</c> kurumun tum katalogunu kapsar; <c>Restricted</c> icin ayrica
/// alan veya egitim erisimi tanimlanmalidir.
/// </param>
public sealed record CreatePlanCommand(
    string Name,
    CatalogAccess CatalogAccess,
    string? Description = null);

public sealed record CreatePlanResult(Guid PlanId, PlanStatus Status);

internal sealed class CreatePlanValidator : AbstractValidator<CreatePlanCommand>
{
    public CreatePlanValidator()
    {
        RuleFor(c => c.Name)
            .NotEmpty().WithErrorCode("billing.plan_name_required")
            .MaximumLength(200).WithErrorCode("billing.plan_name_too_long");

        RuleFor(c => c.CatalogAccess)
            .IsInEnum().WithErrorCode("billing.catalog_access_invalid");

        RuleFor(c => c.Description)
            .MaximumLength(2000).WithErrorCode("billing.plan_description_too_long");
    }
}

/// <summary>
/// Yeni abonelik plani olusturur. Plan taslak olarak baslar.
/// </summary>
/// <remarks>
/// Plan fiyat icermez; fiyatlar ayri bir ucla versiyonlanarak eklenir.
/// </remarks>
public sealed class CreatePlanHandler(
    IPlanRepository plans,
    ICurrentTenant currentTenant,
    IUnitOfWork unitOfWork)
{
    public async Task<Result<CreatePlanResult>> Handle(
        CreatePlanCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (currentTenant.OrganizationId is not { } organizationId)
        {
            return BillingErrors.OrganizationContextRequired;
        }

        var plan = SubscriptionPlan.Create(
            organizationId,
            command.Name,
            command.CatalogAccess,
            command.Description);

        plans.AddPlan(plan);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new CreatePlanResult(plan.Id, plan.Status);
    }
}
