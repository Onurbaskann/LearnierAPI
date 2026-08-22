using Learnier.Application.Common.Abstractions;
using Microsoft.Extensions.Options;

namespace Learnier.Infrastructure.Billing;

internal sealed class PaymentProviderResolver : IPaymentProviderResolver
{
    private readonly Dictionary<string, IPaymentProvider> _providers;

    public PaymentProviderResolver(
        IEnumerable<IPaymentProvider> providers,
        IOptions<PaymentOptions> options)
    {
        _providers = providers.ToDictionary(
            provider => provider.Name,
            StringComparer.OrdinalIgnoreCase);

        if (!_providers.TryGetValue(options.Value.DefaultProvider, out var defaultProvider))
        {
            throw new InvalidOperationException(
                $"Varsayilan odeme saglayicisi kayitli degil: {options.Value.DefaultProvider}");
        }

        DefaultProvider = defaultProvider;
    }

    public IPaymentProvider DefaultProvider { get; }

    public IPaymentProvider? Find(string providerName)
        => _providers.GetValueOrDefault(providerName);
}
