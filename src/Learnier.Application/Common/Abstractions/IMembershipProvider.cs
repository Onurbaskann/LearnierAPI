namespace Learnier.Application.Common.Abstractions;

/// <summary>
/// Kullanicinin bir organizasyondaki uyeligini cozer.
/// </summary>
/// <remarks>
/// Tenant cozumlemesinin guvenlik temeli budur: istek hangi organizasyonu belirtirse
/// belirtsin, kullanicinin o organizasyonda <b>aktif</b> uyeligi yoksa istek reddedilir.
/// Aksi halde organizasyon kimligini degistiren bir istemci baskasinin verisine ulasabilirdi.
/// </remarks>
public interface IMembershipProvider
{
    Task<MembershipInfo?> FindActiveMembership(
        Guid userId,
        Guid organizationId,
        CancellationToken cancellationToken);
}

/// <param name="MembershipId">
/// Uyelik kimligi. Egitmen profili gibi kayitlar kullaniciya degil uyelige baglanir,
/// cunku ayni kisi farkli kurumlarda farkli profillere sahip olabilir.
/// </param>
public sealed record MembershipInfo(Guid MembershipId, Guid OrganizationId, Guid UserId);
