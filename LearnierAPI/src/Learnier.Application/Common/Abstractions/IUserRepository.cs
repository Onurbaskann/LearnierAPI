using Learnier.Domain.Identity;

namespace Learnier.Application.Common.Abstractions;

/// <summary>
/// Kullanici okuma islemleri.
/// </summary>
/// <remarks>
/// Application katmani EF Core'a bagimli olmadigi icin sorgular bu sozlesme
/// arkasindan calisir. Kapsam bilerek dar tutuldu: jenerik bir depo yerine
/// yalnizca ihtiyac duyulan iki islem var.
/// </remarks>
public interface IUserRepository
{
    /// <summary>
    /// E-postaya gore kullaniciyi bulur. Karsilastirma buyuk/kucuk harf duyarsizdir.
    /// </summary>
    /// <remarks>
    /// Donen varlik <b>izlenir</b>: parola ozeti yenilenmesi gerektiginde cagiran
    /// taraf uzerinde degisiklik yapip <see cref="IUnitOfWork"/> ile kaydedebilir.
    /// </remarks>
    Task<User?> FindByEmailAsync(string email, CancellationToken cancellationToken);

    /// <summary>
    /// Kimlige gore kullaniciyi bulur.
    /// </summary>
    /// <remarks>
    /// Token yenilemede kullanilir: yenileme tokeni kullanici kimligini tasir,
    /// ancak hesabin hala kullanilabilir durumda oldugu dogrulanmalidir.
    /// </remarks>
    Task<User?> FindByIdAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// Kullanicinin aktif uyeliklerini, kurum bilgisi ve rol kodlariyla birlikte dondurur.
    /// </summary>
    /// <remarks>
    /// Giris yanitinda tasinir: kullanici birden fazla kurumda uye olabilir ve rolu
    /// her kurumda farklidir, bu yuzden "rol" tek bir alan olarak dondurulemez.
    /// </remarks>
    Task<IReadOnlyList<UserMembership>> GetActiveMembershipsAsync(
        Guid userId,
        CancellationToken cancellationToken);

    void Add(User user);
}

/// <param name="RoleCodes">Uyeligin bu kurumdaki rol kodlari, ornegin <c>student</c>.</param>
public sealed record UserMembership(
    Guid MembershipId,
    Guid OrganizationId,
    string OrganizationName,
    string OrganizationSlug,
    IReadOnlyList<string> RoleCodes);
