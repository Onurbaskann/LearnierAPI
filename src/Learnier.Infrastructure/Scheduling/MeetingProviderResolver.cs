using Learnier.Application.Common.Abstractions;
using Microsoft.Extensions.Options;

namespace Learnier.Infrastructure.Scheduling;

internal sealed class MeetingProviderResolver : IMeetingProviderResolver
{
    private readonly Dictionary<string, IMeetingProvider> _providers;

    public MeetingProviderResolver(
        IEnumerable<IMeetingProvider> providers,
        IOptions<MeetingOptions> options)
    {
        _providers = providers.ToDictionary(provider => provider.Name, StringComparer.OrdinalIgnoreCase);
        if (!_providers.TryGetValue(options.Value.DefaultProvider, out var defaultProvider))
        {
            throw new InvalidOperationException(
                $"Varsayilan meeting saglayicisi kayitli degil: {options.Value.DefaultProvider}");
        }

        DefaultProvider = defaultProvider;
    }

    public IMeetingProvider DefaultProvider { get; }

    public IMeetingProvider? Find(string providerName) => _providers.GetValueOrDefault(providerName);
}
