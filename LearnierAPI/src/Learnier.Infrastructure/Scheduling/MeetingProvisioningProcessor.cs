using Learnier.Application.Common.Abstractions;
using Learnier.Domain.Scheduling;
using Learnier.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Learnier.Infrastructure.Scheduling;

internal sealed class MeetingProvisioningProcessor(
    AppDbContext context,
    IMeetingProviderResolver providerResolver,
    IClock clock,
    IOptions<MeetingOptions> options) : IMeetingProvisioningProcessor
{
    public async Task<int> ProcessBatchAsync(CancellationToken cancellationToken)
    {
        var settings = options.Value;
        var meetings = await context.Meetings
            .Include(meeting => meeting.Session)
                .ThenInclude(session => session.Course)
            .Where(meeting => (meeting.Status == MeetingStatus.Pending
                               || meeting.Status == MeetingStatus.Failed)
                              && meeting.ProvisioningAttemptCount < settings.MaxAttempts)
            .OrderBy(meeting => meeting.CreatedAt)
            .ThenBy(meeting => meeting.Id)
            .Take(settings.BatchSize)
            .ToListAsync(cancellationToken);

        foreach (var meeting in meetings)
        {
            meeting.StartProvisioning();
            await context.SaveChangesAsync(cancellationToken);

            var provider = providerResolver.Find(meeting.Provider);
            if (provider is null)
            {
                meeting.MarkFailed($"Meeting saglayicisi kayitli degil: {meeting.Provider}");
                await context.SaveChangesAsync(cancellationToken);
                continue;
            }

            try
            {
                var result = await provider.CreateMeetingAsync(
                    new ProviderMeetingRequest(
                        meeting.Id,
                        meeting.SessionId,
                        meeting.Session.Course.Title,
                        meeting.StartsAt,
                        meeting.EndsAt,
                        "Europe/Istanbul"),
                    cancellationToken);

                meeting.MarkReady(
                    result.ProviderMeetingId,
                    result.JoinUrl,
                    result.HostUrl,
                    clock.UtcNow);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                meeting.MarkFailed(exception.Message);
            }

            await context.SaveChangesAsync(cancellationToken);
        }

        return meetings.Count;
    }

    public async Task<int> ProcessCancellationBatchAsync(CancellationToken cancellationToken)
    {
        var settings = options.Value;
        var meetings = await context.Meetings
            .Where(meeting => meeting.Status == MeetingStatus.Cancelled
                              && meeting.ProviderMeetingId != null
                              && meeting.ProviderCancelledAt == null
                              && meeting.CancellationAttemptCount < settings.MaxAttempts)
            .OrderBy(meeting => meeting.CancelledAt)
            .ThenBy(meeting => meeting.Id)
            .Take(settings.BatchSize)
            .ToListAsync(cancellationToken);

        foreach (var meeting in meetings)
        {
            meeting.StartCancellationAttempt();
            await context.SaveChangesAsync(cancellationToken);

            var provider = providerResolver.Find(meeting.Provider);
            if (provider is null)
            {
                meeting.MarkProviderCancellationFailed(
                    $"Meeting saglayicisi kayitli degil: {meeting.Provider}");
                await context.SaveChangesAsync(cancellationToken);
                continue;
            }

            try
            {
                await provider.CancelMeetingAsync(
                    meeting.ProviderMeetingId!,
                    cancellationToken);
                meeting.MarkProviderCancelled(clock.UtcNow);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                meeting.MarkProviderCancellationFailed(exception.Message);
            }

            await context.SaveChangesAsync(cancellationToken);
        }

        return meetings.Count;
    }
}
