using Learnier.Application.Common.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Learnier.Infrastructure.Scheduling;

internal sealed partial class MeetingProvisioningWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<MeetingOptions> options,
    ILogger<MeetingProvisioningWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.ProvisioningEnabled)
        {
            return;
        }

        using var timer = new PeriodicTimer(
            TimeSpan.FromMinutes(options.Value.ProvisioningIntervalMinutes));

        do
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var processor = scope.ServiceProvider
                    .GetRequiredService<IMeetingProvisioningProcessor>();
                await processor.ProcessBatchAsync(stoppingToken);
                await processor.ProcessCancellationBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                LogBatchFailed(exception);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    [LoggerMessage(
        EventId = 2101,
        Level = LogLevel.Error,
        Message = "Meeting provisioning batch failed.")]
    private partial void LogBatchFailed(Exception exception);
}
