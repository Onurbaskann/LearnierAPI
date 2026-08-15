using FluentValidation;
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

internal sealed class AddPlanPriceValidator : AbstractValidator<AddPlanPriceCommand>
{
    public AddPlanPriceValidator()
    {
        RuleFor(c => c.Currency)
            .NotEmpty().WithErrorCode("billing.currency_required")
            .Length(3).WithErrorCode("billing.currency_invalid");

        RuleFor(c => c.Amount)
            .GreaterThanOrEqualTo(0).WithErrorCode("billing.amount_invalid");

        RuleFor(c => c.BillingInterval)
            .IsInEnum().WithErrorCode("billing.billing_interval_invalid");

        RuleFor(c => c.BillingIntervalCount)
            .InclusiveBetween(1, 36).WithErrorCode("billing.billing_interval_count_invalid");
    }
}

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
/// </remarks>
public sealed class AddPlanPriceHandler(
    IBillingRepository billing,
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

        var plan = await billing.FindPlanAsync(command.PlanId, includeDetails: true, cancellationToken);

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
