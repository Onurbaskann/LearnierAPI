using Learnier.Application.Common.Abstractions;
using Learnier.Domain.Billing;
using Microsoft.EntityFrameworkCore;

namespace Learnier.Infrastructure.Persistence.Repositories;

internal sealed class EfClubAccessPolicy(AppDbContext context, IClock clock) : IClubAccessPolicy
{
    public async Task<bool> CanAccessSubjectAsync(
        Guid userId,
        Guid subjectId,
        CancellationToken cancellationToken)
        => await context.Subscriptions.AnyAsync(
            subscription =>
                subscription.Status == SubscriptionStatus.Active
                && subscription.CurrentPeriodStart <= clock.UtcNow
                && subscription.CurrentPeriodEnd > clock.UtcNow
                && (subscription.SubscriberUserId == userId
                    || subscription.Seats.Any(seat =>
                        seat.Membership.UserId == userId && seat.RevokedAt == null))
                && (subscription.PlanPrice.Plan.CatalogAccess == CatalogAccess.All
                    || context.PlanSubjectAccess.Any(access =>
                        access.PlanId == subscription.PlanPrice.PlanId
                        && access.SubjectId == subjectId)),
            cancellationToken);
}
