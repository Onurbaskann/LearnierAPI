using Learnier.Domain.Common;
using Learnier.Domain.Identity;

namespace Learnier.Domain.Billing;

/// <summary>
/// Odeme saglayicisindaki musteri kaydinin karsiligi.
/// </summary>
/// <remarks>
/// Kart bilgisi burada tutulmaz. Yalnizca saglayicinin verdigi musteri kimligi
/// saklanir; kart, token ve CVV saglayicinin tarafinda kalir.
/// </remarks>
public sealed class PaymentCustomer : Entity, IAuditableEntity
{
    private PaymentCustomer()
    {
        Provider = string.Empty;
        ProviderCustomerId = string.Empty;
    }

    public Guid? UserId { get; private set; }

    /// <summary>Kurumsal musteri kaydinda dolu olur.</summary>
    public Guid? OrganizationId { get; private set; }

    public string Provider { get; private set; }

    public string ProviderCustomerId { get; private set; }

    public User? User { get; private set; }

    public DateTimeOffset CreatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    public Guid? UpdatedBy { get; set; }

    public static PaymentCustomer CreateForUser(Guid userId, string provider, string providerCustomerId)
        => Create(userId, null, provider, providerCustomerId);

    public static PaymentCustomer CreateForOrganization(
        Guid organizationId,
        string provider,
        string providerCustomerId)
        => Create(null, organizationId, provider, providerCustomerId);

    private static PaymentCustomer Create(
        Guid? userId,
        Guid? organizationId,
        string provider,
        string providerCustomerId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(providerCustomerId);

        if (userId is null == organizationId is null)
        {
            throw new ArgumentException(
                "Odeme musterisi ya bir kullaniciya ya bir kuruma ait olmalidir.",
                nameof(userId));
        }

        return new PaymentCustomer
        {
            UserId = userId,
            OrganizationId = organizationId,
            Provider = provider.Trim().ToLowerInvariant(),
            ProviderCustomerId = providerCustomerId.Trim()
        };
    }
}
