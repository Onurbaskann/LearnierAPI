using Learnier.Application.Common.Abstractions;

namespace Learnier.Infrastructure.Identity.Placeholders;

/// <summary>
/// GECICI: hicbir uyeligi dogrulamaz.
/// </summary>
/// <remarks>
/// <para>
/// Gercek uygulama <c>organization_memberships</c> tablosunu sorgular; o tablo Faz 1'de
/// olusturulacak. Bu tip Faz 1'de <c>MembershipProvider</c> ile degistirilmelidir.
/// </para>
/// <para>
/// Bilerek <b>kapali varsayilan</b>: eksik implementasyon "herkese izin ver" degil
/// "kimseye izin verme" anlamina gelir. Yanlislikla uretime ciksa bile veri sizmasi olusmaz,
/// yalnizca istekler reddedilir - yani hata gurultulu ve fark edilir olur.
/// </para>
/// </remarks>
internal sealed class DenyAllMembershipProvider : IMembershipProvider
{
    public Task<MembershipInfo?> FindActiveMembership(
        Guid userId,
        Guid organizationId,
        CancellationToken cancellationToken)
        => Task.FromResult<MembershipInfo?>(null);
}
