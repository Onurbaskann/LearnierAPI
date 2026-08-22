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

/// <summary>
/// Plana hak tanimi ekler.
/// </summary>
/// <remarks>
/// <para>
/// Ornekler: "sinirsiz grup dersi" (<c>BookingAccess</c> + <c>Group</c>),
/// "ayda 4 birebir ders" (<c>LessonCredit</c> + <c>Private</c> + 4 + <c>Month</c>).
/// </para>
/// <para>
/// Adet kontrolleri FluentValidation ile degil burada yapiliyor: plan kimligi
/// rotadan geldigi icin komut action icinde kuruluyor ve <c>ValidationFilter</c>
/// yalnizca action parametrelerini gorebiliyor.
/// </para>
/// </remarks>
public sealed class AddPlanEntitlementHandler(
    IPlanRepository plans,
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

        switch (command.EntitlementType)
        {
            // Sayili hakta adet zorunlu: aksi halde "kac ders" sorusu yanitsiz kalir.
            case EntitlementType.LessonCredit when command.Quantity is null:
                return BillingErrors.QuantityRequired;

            case EntitlementType.LessonCredit when command.Quantity < 1:
                return BillingErrors.QuantityInvalid;

            // Sinirsiz erisimde adet anlamsiz; verilmesi karisikliga yol acar.
            case EntitlementType.BookingAccess when command.Quantity is not null:
                return BillingErrors.QuantityNotAllowed;

            default:
                break;
        }

        var plan = await plans.FindPlanAsync(command.PlanId, includeDetails: true, cancellationToken);

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
