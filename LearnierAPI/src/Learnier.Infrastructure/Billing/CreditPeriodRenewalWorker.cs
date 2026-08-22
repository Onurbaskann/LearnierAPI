using Learnier.Application.Common.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Learnier.Infrastructure.Billing;

internal sealed partial class CreditPeriodRenewalWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<CreditRenewalOptions> options,
    ILogger<CreditPeriodRenewalWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled)
        {
            return;
        }

        using var timer = new PeriodicTimer(
            TimeSpan.FromMinutes(options.Value.IntervalMinutes));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var processor = scope.ServiceProvider
                    .GetRequiredService<ICreditPeriodRenewalProcessor>();
                var result = await processor.ProcessDueAsync(
                    options.Value.BatchSize,
                    stoppingToken);

                if (result.RenewedPeriods > 0 || result.EndedSubscriptions > 0)
                {
                    LogRenewalCompleted(
                        logger,
                        result.ScannedSubscriptions,
                        result.RenewedPeriods,
                        result.ExpiredCredits,
                        result.GrantedCredits,
                        result.EndedSubscriptions);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                LogRenewalFailed(logger, exception);
            }
        }
    }

    [LoggerMessage(
        EventId = 2101,
        Level = LogLevel.Information,
        Message = "Credit periods renewed. Scanned={Scanned}, Renewed={Renewed}, "
                  + "ExpiredCredits={Expired}, GrantedCredits={Granted}, Ended={Ended}")]
    private static partial void LogRenewalCompleted(
        ILogger logger,
        int scanned,
        int renewed,
        int expired,
        int granted,
        int ended);

    [LoggerMessage(
        EventId = 2102,
        Level = LogLevel.Error,
        Message = "Credit period renewal failed.")]
    private static partial void LogRenewalFailed(ILogger logger, Exception exception);
}
