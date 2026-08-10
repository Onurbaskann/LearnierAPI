using Learnier.Domain.Identity;

namespace Learnier.Application.Common.Abstractions;

/// <summary>
/// Acik kayitla olusan kullaniciyi yapilandirilmis kuruma ogrenci olarak ekler.
/// </summary>
public interface IRegistrationMembershipProvisioner
{
    Task ProvisionAsync(User user, CancellationToken cancellationToken);
}
