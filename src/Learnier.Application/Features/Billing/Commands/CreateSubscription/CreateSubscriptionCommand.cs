using FluentValidation;
using Learnier.Application.Common.Abstractions;
using Learnier.Application.Common.Results;
using Learnier.Domain.Billing;

namespace Learnier.Application.Features.Billing.Commands.CreateSubscription;

/// <param name="SubscriberUserId">Bireysel abonelikte dolu.</param>
/// <param name="SubscriberOrganizationId">Kurumsal abonelikte dolu.</param>
public sealed record CreateSubscriptionCommand(
    Guid PlanPriceId,
    Guid? SubscriberUserId = null,
    Guid? SubscriberOrganizationId = null);

public sealed record CreateSubscriptionResult(
    Guid SubscriptionId,
    SubscriptionStatus Status,
    DateTimeOffset CurrentPeriodEnd,
    int GrantedCreditCount);

internal sealed class CreateSubscriptionValidator : AbstractValidator<CreateSubscriptionCommand>
{
    public CreateSubscriptionValidator()
    {
        RuleFor(c => c.PlanPriceId)
            .NotEmpty().WithErrorCode("billing.plan_price_required");

        // Tam olarak bir sahip: kaynak dokumanin 8. bolumu bunu check constraint
        // ile de zorunlu kiliyor, burada anlamli bir hata mesaji icin dogrulaniyor.
        RuleFor(c => c)
            .Must(c => c.SubscriberUserId is not null ^ c.SubscriberOrganizationId is not null)
            .WithErrorCode("billing.subscriber_invalid");
    }
}
