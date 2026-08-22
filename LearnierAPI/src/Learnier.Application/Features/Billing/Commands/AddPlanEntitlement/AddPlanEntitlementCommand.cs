using Learnier.Application.Common.Abstractions;
using Learnier.Application.Common.Results;
using Learnier.Domain.Billing;
using Learnier.Domain.Scheduling;

namespace Learnier.Application.Features.Billing.Commands.AddPlanEntitlement;

/// <param name="Quantity">
/// <c>LessonCredit</c> icin zorunlu. <c>BookingAccess</c> sinirsiz erisimi ifade
/// ettigi icin bos birakilir.
/// </param>
/// <param name="LessonDurationMinutes">
/// Birebir ders kredisinde zorunlu: 30 veya 50. Rezervasyon yetkilendirmesi uygun
/// paketi bu alanla secer. Diger haklarda bos birakilir.
/// </param>
public sealed record AddPlanEntitlementCommand(
    Guid PlanId,
    EntitlementType EntitlementType,
    SessionType SessionType,
    EntitlementResetPeriod ResetPeriod,
    int? Quantity = null,
    int? LessonDurationMinutes = null);

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

        // Suresi olmayan birebir kredi hicbir oturumla eslesmez; sureli grup hakki
        // ise hicbir yerde okunmaz.
        var isPrivateCredit = command.EntitlementType is EntitlementType.LessonCredit
            && command.SessionType is SessionType.Private;

        if (isPrivateCredit)
        {
            if (command.LessonDurationMinutes is null)
            {
                return BillingErrors.LessonDurationRequired;
            }

            if (command.LessonDurationMinutes is not (30 or 50))
            {
                return BillingErrors.LessonDurationInvalid;
            }
        }
        else if (command.LessonDurationMinutes is not null)
        {
            return BillingErrors.LessonDurationNotAllowed;
        }

        var plan = await plans.FindPlanAsync(command.PlanId, includeDetails: true, cancellationToken);

        if (plan is null)
        {
            return BillingErrors.PlanNotFound;
        }

        // Benzersiz indeks ihlali 500'e donusmeden once anlasilir bir cakisma dondurulur.
        var alreadyDefined = plan.Entitlements.Any(existing =>
            existing.EntitlementType == command.EntitlementType
            && existing.SessionType == command.SessionType
            && existing.LessonDurationMinutes == command.LessonDurationMinutes);

        if (alreadyDefined)
        {
            return BillingErrors.EntitlementAlreadyExists;
        }

        var entitlement = plan.AddEntitlement(
            command.EntitlementType,
            command.SessionType,
            command.Quantity,
            command.ResetPeriod,
            command.LessonDurationMinutes);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new AddPlanEntitlementResult(entitlement.Id);
    }
}
