using Learnier.Application.Common.Abstractions;
using Learnier.Application.Common.Results;
using Learnier.Domain.Billing;

namespace Learnier.Application.Features.Billing.Commands.AddPlanPrice;

/// <param name="BillingIntervalCount">
/// Kac aralikta bir tahsil edilecegi - "3 ayda bir" icin <c>Month</c> ve 3.
/// </param>
public sealed record AddPlanPriceCommand(
    Guid PlanId,
    string Currency,
    decimal Amount,
    BillingInterval BillingInterval,
    int BillingIntervalCount);

public sealed record AddPlanPriceResult(Guid PlanPriceId, Guid? ArchivedPriceId);

/// <summary>
/// Plana yeni fiyat surumu ekler.
/// </summary>
/// <remarks>
/// <para>
/// Mevcut fiyat <b>guncellenmez</b>, arsivlenir ve yerine yeni kayit acilir.
/// Kaynak dokumanin 8. bolumunun gerekcesi: eski aboneliklerin hangi tutardan
/// satildigi izlenebilir kalmali. Fiyat guncellenseydi gecmis faturalarla
/// veritabani celisirdi.
/// </para>
/// <para>
/// Girdi kontrolleri FluentValidation ile degil burada yapiliyor: plan kimligi
/// rotadan geldigi icin komut action icinde kuruluyor ve <c>ValidationFilter</c>
/// yalnizca action parametrelerini gorebiliyor.
/// </para>
/// </remarks>
public sealed class AddPlanPriceHandler(
    IPlanRepository plans,
    ICurrentTenant currentTenant,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    public async Task<Result<AddPlanPriceResult>> Handle(
        AddPlanPriceCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!currentTenant.HasTenant)
        {
            return BillingErrors.OrganizationContextRequired;
        }

        if (command.Currency?.Trim().Length is not 3)
        {
            return BillingErrors.CurrencyInvalid;
        }

        if (command.Amount < 0)
        {
            return BillingErrors.AmountInvalid;
        }

        if (command.BillingIntervalCount is < 1 or > 36)
        {
            return BillingErrors.BillingIntervalCountInvalid;
        }

        var plan = await plans.FindPlanAsync(command.PlanId, includeDetails: true, cancellationToken);

        if (plan is null)
        {
            return BillingErrors.PlanNotFound;
        }

        var normalizedCurrency = command.Currency.Trim().ToUpperInvariant();

        // Arsivlenecek kayit, yeni fiyat eklenmeden once tespit edilir; sonra
        // hangisinin kapatildigini soylemek mumkun olmazdi.
        var replaced = plan.Prices.FirstOrDefault(p =>
            p.Status is PlanPriceStatus.Active
            && p.Currency == normalizedCurrency
            && p.BillingInterval == command.BillingInterval
            && p.BillingIntervalCount == command.BillingIntervalCount);

        var price = plan.AddPrice(
            normalizedCurrency,
            command.Amount,
            command.BillingInterval,
            command.BillingIntervalCount,
            clock.UtcNow);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new AddPlanPriceResult(price.Id, replaced?.Id);
    }
}
