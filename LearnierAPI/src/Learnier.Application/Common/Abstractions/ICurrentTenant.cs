namespace Learnier.Application.Common.Abstractions;

/// <summary>
/// Istegin uzerinde calistigi organizasyon (kiraci).
/// </summary>
/// <remarks>
/// Bir kullanici birden fazla organizasyonda uye olabilir ve her birinde farkli rol
/// tasiyabilir (bir kurumda egitmen, digerinde ogrenci). Bu yuzden "aktif organizasyon"
/// kullanicidan degil istekten cozulur ve uyelik <c>TenantResolutionMiddleware</c>
/// tarafindan dogrulanir.
/// </remarks>
public interface ICurrentTenant
{
    /// <summary>
    /// Aktif organizasyon kimligi. Organizasyon kapsami disindaki isteklerde
    /// (ornegin giris, kayit) <see langword="null"/>.
    /// </summary>
    Guid? OrganizationId { get; }

    /// <summary>
    /// Aktif organizasyondaki uyelik kimligi. Egitmen profili gibi kayitlar
    /// kullaniciya degil uyelige baglanir.
    /// </summary>
    Guid? MembershipId { get; }

    bool HasTenant { get; }
}
