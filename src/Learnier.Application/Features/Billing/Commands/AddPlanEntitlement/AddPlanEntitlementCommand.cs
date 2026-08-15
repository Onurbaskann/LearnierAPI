using FluentValidation;
using Learnier.Application.Common.Abstractions;
using Learnier.Application.Common.Results;
using Learnier.Domain.Billing;
using Learnier.Domain.Scheduling;

namespace Learnier.Application.Features.Billing.Commands.AddPlanEntitlement;

/// <param name="Quantity">
/// <c>LessonCredit</c> icin zorunlu. <c>BookingAccess</c> sinirsiz erisimi ifade
/// ettigi icin bos birakilir.
/// </param>
public sealed record AddPlanEntitlementCommand(
    Guid PlanId,
    EntitlementType EntitlementType,
    SessionType SessionType,
    EntitlementResetPeriod ResetPeriod,
    int? Quantity = null);

public sealed record AddPlanEntitlementResult(Guid EntitlementId);

internal sealed class AddPlanEntitlementValidator : AbstractValidator<AddPlanEntitlementCommand>
{
    public AddPlanEntitlementValidator()
    {
        RuleFor(c => c.EntitlementType)
            .IsInEnum().WithErrorCode("billing.entitlement_type_invalid");

        RuleFor(c => c.SessionType)
            .IsInEnum().WithErrorCode("billing.session_type_invalid");

        RuleFor(c => c.ResetPeriod)
            .IsInEnum().WithErrorCode("billing.reset_period_invalid");

        // Sayili hakta miktar zorunlu: aksi halde "kac ders" sorusu yanitsiz kalir.
        RuleFor(c => c.Quantity)
            .NotNull().WithErrorCode("billing.quantity_required")
            .GreaterThan(0).WithErrorCode("billing.quantity_invalid")
            .When(c => c.EntitlementType is EntitlementType.LessonCredit);

        // Sinirsiz erisimde miktar anlamsiz; verilmesi karisikliga yol acar.
        RuleFor(c => c.Quantity)
            .Null().WithErrorCode("billing.quantity_not_allowed")
            .When(c => c.EntitlementType is EntitlementType.BookingAccess);
    }
}

/// <summary>
/// Plana hak tanimi ekler.
/// </summary>
/// <remarks>
/// Ornekler: "sinirsiz grup dersi" (<c>BookingAccess</c> + <c>Group</c>),
/// "ayda 4 birebir ders" (<c>LessonCredit</c> + <c>Private</c> + 4 + <c>Month</c>).
/// </remarks>
public sealed class AddPlanEntitlementHandler(
    IBillingRepository billing,
    ICurrentTenant currentTenant,
    IUnitOfWork unitOfWork)
{
    public async Task<Result<AddPlanEntitlementResult>> Handle(
        AddPlanEntitlementCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!currentTenant.HasTenant)
        {
            return BillingErrors.OrganizationContextRequired;
        }

        var plan = await billing.FindPlanAsync(command.PlanId, includeDetails: true, cancellationToken);

        if (plan is null)
        {
            return BillingErrors.PlanNotFound;
        }

        var entitlement = plan.AddEntitlement(
            command.EntitlementType,
            command.SessionType,
            command.Quantity,
            command.ResetPeriod);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new AddPlanEntitlementResult(entitlement.Id);
    }
}
