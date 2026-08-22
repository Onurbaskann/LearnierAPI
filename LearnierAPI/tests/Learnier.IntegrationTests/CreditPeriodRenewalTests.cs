using Learnier.Application.Common.Abstractions;
using Learnier.Domain.Billing;
using Learnier.Infrastructure.Persistence.Seeding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Learnier.IntegrationTests;

public sealed class CreditPeriodRenewalTests(PostgresFixture postgres)
    : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task DuePeriod_ExpiresUnusedCredits_AndGrantsNextMonthOnce()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var provider = TestServices.BuildProvider(postgres);
        await DatabaseSeeder.RunAsync(provider, includeDevelopmentData: true, cancellationToken);

        Guid subscriptionId;
        DateTimeOffset oldPeriodStart;
        DateTimeOffset oldPeriodEnd;

        await using (var context = postgres.CreateContext())
        {
            subscriptionId = await context.Subscriptions
                .Where(subscription => subscription.SubscriberUser!.Email == "ogrenci@hotmail.com")
                .Select(subscription => subscription.Id)
                .SingleAsync(cancellationToken);

            oldPeriodStart = DateTimeOffset.UtcNow.AddMonths(-1);
            oldPeriodEnd = DateTimeOffset.UtcNow.AddMinutes(-1);

            await context.CreditLedger
                .Where(entry => entry.SubscriptionId == subscriptionId
                                && entry.TransactionType == CreditTransactionType.PeriodGrant)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(entry => entry.PeriodStart, (DateTimeOffset?)null)
                    .SetProperty(entry => entry.CreatedAt, oldPeriodStart)
                    .SetProperty(entry => entry.ExpiresAt, oldPeriodEnd),
                    cancellationToken);
        }

        await using (var scope = provider.CreateAsyncScope())
        {
            var processor = scope.ServiceProvider
                .GetRequiredService<ICreditPeriodRenewalProcessor>();

            var first = await processor.ProcessDueAsync(100, cancellationToken);
            first.RenewedPeriods.ShouldBe(1);
            first.ExpiredCredits.ShouldBe(12);
            first.GrantedCredits.ShouldBe(12);

            var second = await processor.ProcessDueAsync(100, cancellationToken);
            second.RenewedPeriods.ShouldBe(0);
            second.ExpiredCredits.ShouldBe(0);
            second.GrantedCredits.ShouldBe(0);
        }

        await using (var context = postgres.CreateContext())
        {
            var oldPeriodBalance = await context.CreditLedger
                .Where(entry => entry.SubscriptionId == subscriptionId
                                && (entry.PeriodStart == null
                                    || entry.PeriodStart == oldPeriodStart))
                .SumAsync(entry => entry.Quantity, cancellationToken);
            oldPeriodBalance.ShouldBe(0);

            var grants = await context.CreditLedger
                .Where(entry => entry.SubscriptionId == subscriptionId
                                && entry.TransactionType == CreditTransactionType.PeriodGrant)
                .ToListAsync(cancellationToken);
            grants.Count.ShouldBe(2);
            var renewedGrant = grants.Single(entry => entry.PeriodStart is not null);
            renewedGrant.PeriodStart!.Value.ShouldBe(
                oldPeriodEnd,
                tolerance: TimeSpan.FromMilliseconds(1));
            renewedGrant.Quantity.ShouldBe(12);
        }
    }
}
