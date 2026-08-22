using Learnier.Application.Common.Abstractions;
using Microsoft.Extensions.Options;

namespace Learnier.Infrastructure.Scheduling;

public sealed class SandboxMeetingProvider(IOptions<MeetingOptions> options) : IMeetingProvider
{
    public string Name => "sandbox";

    public Task<ProviderMeetingResult> CreateMeetingAsync(
        ProviderMeetingRequest request,
        CancellationToken cancellationToken)
    {
        var providerMeetingId = $"sandbox-meeting-{request.MeetingId:N}";
        var baseUrl = options.Value.PublicApiBaseUrl.TrimEnd('/');

        return Task.FromResult(new ProviderMeetingResult(
            providerMeetingId,
            $"{baseUrl}/api/v1/meetings/sandbox/{request.MeetingId}/join",
            $"{baseUrl}/api/v1/meetings/sandbox/{request.MeetingId}/host"));
    }

    public Task CancelMeetingAsync(
        string providerMeetingId,
        CancellationToken cancellationToken)
        => Task.CompletedTask;
}
