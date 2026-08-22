using Learnier.Application.Common.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Learnier.Infrastructure.Scheduling;

internal sealed partial class SessionCompletionWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<SessionCompletionOptions> options,
    ILogger<SessionCompletionWorker> logger) : BackgroundService
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
                    .GetRequiredService<ISessionCompletionProcessor>();
                var result = await processor.ProcessDueAsync(
                    options.Value.BatchSize,
                    TimeSpan.FromMinutes(options.Value.GracePeriodMinutes),
                    stoppingToken);

                if (result.CompletedSessions > 0 || result.SkippedSessions > 0)
                {
                    LogCompletionFinished(
                        logger,
                        result.ScannedSessions,
                        result.CompletedSessions,
                        result.CompletedBookings,
                        result.SkippedSessions);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                LogCompletionFailed(logger, exception);
            }
        }
    }

    [LoggerMessage(
        EventId = 2201,
        Level = LogLevel.Information,
        Message = "Sessions auto-completed. Scanned={Scanned}, Completed={Completed}, "
                  + "Bookings={Bookings}, Skipped={Skipped}")]
    private static partial void LogCompletionFinished(
        ILogger logger,
        int scanned,
        int completed,
        int bookings,
        int skipped);

    [LoggerMessage(
        EventId = 2202,
        Level = LogLevel.Error,
        Message = "Session auto-completion failed.")]
    private static partial void LogCompletionFailed(ILogger logger, Exception exception);
}
