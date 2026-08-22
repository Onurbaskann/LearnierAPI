using FluentValidation;
using Learnier.Application.Common.Abstractions;
using Learnier.Application.Common.Results;
using Learnier.Domain.Billing;

namespace Learnier.Application.Features.Billing.Commands.CreateCheckout;

public sealed record CreateCheckoutCommand(Guid PlanPriceId, string IdempotencyKey);

public sealed record CreateCheckoutResult(
    Guid CheckoutSessionId,
    string Provider,
    string CheckoutUrl,
    DateTimeOffset ExpiresAt);

internal sealed class CreateCheckoutValidator : AbstractValidator<CreateCheckoutCommand>
{
    public CreateCheckoutValidator()
    {
        RuleFor(c => c.PlanPriceId)
            .NotEmpty().WithErrorCode("billing.plan_price_not_found");

        RuleFor(c => c.IdempotencyKey)
            .NotEmpty().WithErrorCode("payment.idempotency_key_required")
            .MaximumLength(200).WithErrorCode("payment.idempotency_key_too_long");
    }
}

public sealed class CreateCheckoutHandler(
    ISubscriptionPurchaseRepository purchaseRepository,
    IPaymentOrchestrationRepository paymentRepository,
    IPaymentProviderResolver providerResolver,
    IUserRepository userRepository,
    ICurrentUser currentUser,
    ICurrentTenant currentTenant,
    IClock clock,
    IUnitOfWork unitOfWork)
{
    public async Task<Result<CreateCheckoutResult>> Handle(
        CreateCheckoutCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (currentUser.UserId is not { } userId)
        {
            return Error.Unauthorized("common.unauthorized");
        }

        if (currentTenant.OrganizationId is not { } organizationId)
        {
            return BillingErrors.OrganizationContextRequired;
        }

        var provider = providerResolver.DefaultProvider;
        var idempotencyKey = command.IdempotencyKey.Trim();
        var existing = await paymentRepository.FindCheckoutByIdempotencyKeyAsync(
            provider.Name,
            idempotencyKey,
            cancellationToken);

        if (existing is not null)
        {
            if (existing.UserId != userId || existing.PlanPriceId != command.PlanPriceId)
            {
                return PaymentErrors.CheckoutNotReady;
            }

            if (existing.Status is CheckoutSessionStatus.Ready
                && existing.CheckoutUrl is not null)
            {
                return ToResult(existing);
            }

            // Saglayici cagrisi basarisiz olup ilk Save tamamlandiysa kayit Created
            // kalir. Ayni idempotency anahtari bu yarim kalmis islemi guvenle devam
            // ettirir; ikinci bir checkout satiri acmaz.
            if (existing.Status is CheckoutSessionStatus.Created
                && existing.ExpiresAt > clock.UtcNow)
            {
                var existingUser = await userRepository.FindByIdAsync(userId, cancellationToken);
                if (existingUser is null)
                {
                    return Error.Unauthorized("common.unauthorized");
                }

                return await PrepareProviderCheckout(
                    existing,
                    existingUser.Email,
                    provider,
                    cancellationToken);
            }

            return PaymentErrors.CheckoutNotReady;
        }

        var plan = await purchaseRepository.FindPlanByPriceAsync(
            command.PlanPriceId,
            cancellationToken);

        if (plan is null)
        {
            return BillingErrors.PlanPriceNotFound;
        }

        var price = plan.Prices.Single(p => p.Id == command.PlanPriceId);
        if (price.Status is not PlanPriceStatus.Active)
        {
            return BillingErrors.PlanPriceNotActive;
        }

        if (plan.Status is not PlanStatus.Active)
        {
            return BillingErrors.PlanNotActive;
        }

        if (plan.IsSystemGenerated)
        {
            return BillingErrors.PlanNotPurchasable;
        }

        if (plan.Entitlements.Count is 0)
        {
            return BillingErrors.PlanHasNoEntitlement;
        }

        if (price.Amount <= 0)
        {
            return PaymentErrors.PaidPlanRequired;
        }

        var now = clock.UtcNow;
        if (await purchaseRepository.HasActiveSubscriptionAsync(
                userId, plan.Id, now, cancellationToken))
        {
            return BillingErrors.AlreadySubscribed;
        }

        var user = await userRepository.FindByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            return Error.Unauthorized("common.unauthorized");
        }

        var expiresAt = now.Add(provider.CheckoutLifetime);
        var checkout = CheckoutSession.Create(
            organizationId,
            userId,
            price.Id,
            price.Amount,
            price.Currency,
            provider.Name,
            idempotencyKey,
            now,
            expiresAt);

        paymentRepository.AddCheckout(checkout);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return await PrepareProviderCheckout(
            checkout,
            user.Email,
            provider,
            cancellationToken);
    }

    private async Task<CreateCheckoutResult> PrepareProviderCheckout(
        CheckoutSession checkout,
        string customerEmail,
        IPaymentProvider provider,
        CancellationToken cancellationToken)
    {
        var providerResult = await provider.CreateCheckoutAsync(
            new ProviderCheckoutRequest(
                checkout.Id,
                checkout.UserId,
                checkout.PlanPriceId,
                checkout.Amount,
                checkout.Currency,
                customerEmail,
                checkout.ExpiresAt,
                checkout.IdempotencyKey),
            cancellationToken);

        checkout.MarkReady(
            providerResult.ProviderCheckoutSessionId,
            providerResult.CheckoutUrl);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToResult(checkout);
    }

    private static CreateCheckoutResult ToResult(CheckoutSession checkout)
        => new(
            checkout.Id,
            checkout.Provider,
            checkout.CheckoutUrl!,
            checkout.ExpiresAt);
}
