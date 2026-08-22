namespace Learnier.Application.Common.Abstractions;

public interface IMeetingProvider
{
    string Name { get; }

    Task<ProviderMeetingResult> CreateMeetingAsync(
        ProviderMeetingRequest request,
        CancellationToken cancellationToken);

    Task CancelMeetingAsync(string providerMeetingId, CancellationToken cancellationToken);
}

public sealed record ProviderMeetingRequest(
    Guid MeetingId,
    Guid SessionId,
    string Topic,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    string TimeZoneId);

public sealed record ProviderMeetingResult(
    string ProviderMeetingId,
    string JoinUrl,
    string HostUrl);

public interface IMeetingProviderResolver
{
    IMeetingProvider DefaultProvider { get; }

    IMeetingProvider? Find(string providerName);
}
