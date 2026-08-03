using Learnier.Application.Common.Abstractions;
using Learnier.Domain.Identity;
using Learnier.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Learnier.Infrastructure.Identity;

/// <summary>
/// Uyeligi <c>organization_memberships</c> tablosundan cozer.
/// </summary>
/// <remarks>
/// <para>
/// Tenant izolasyonunun giris kapisi burasidir: organizasyon kimligini istemci
/// gonderir, bu sinif da kullanicinin o organizasyonda gercekten aktif uyeligi
/// oldugunu veritabanindan dogrular. Dogrulanmazsa istek reddedilir.
/// </para>
/// <para>
/// Sorgu organizasyon filtresini <b>bilerek</b> devre disi birakir: filtrenin
/// okudugu aktif organizasyon henuz belirlenmemistir - onu belirleyen sorgu budur.
/// Filtre acik kalsaydi tanim geregi kendi kendine bagimli olurdu.
/// </para>
/// </remarks>
internal sealed class EfMembershipProvider(AppDbContext context) : IMembershipProvider
{
    public async Task<MembershipInfo?> FindActiveMembership(
        Guid userId,
        Guid organizationId,
        CancellationToken cancellationToken)
        => await context.Memberships
            .IgnoreQueryFilters([AppDbContext.TenantFilterName])
            .AsNoTracking()
            .Where(m => m.UserId == userId
                        && m.OrganizationId == organizationId
                        && m.Status == MembershipStatus.Active
                        // Askiya alinmis kurum veya kullanici, uyeligi aktif olsa bile
                        // erisemez. Bu kontrol olmasaydi askiya alma islemi elde
                        // gecerli token bulunan kullanicilar icin etkisiz kalirdi.
                        && m.Organization.Status == OrganizationStatus.Active
                        && m.User.Status == UserStatus.Active)
            .Select(m => new MembershipInfo(m.Id, m.OrganizationId, m.UserId))
            .FirstOrDefaultAsync(cancellationToken);
}
