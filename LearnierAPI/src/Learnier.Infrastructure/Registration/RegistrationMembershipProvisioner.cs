using Learnier.Application.Common.Abstractions;
using Learnier.Application.Common.Security;
using Learnier.Domain.Identity;
using Learnier.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Learnier.Infrastructure.Registration;

internal sealed class RegistrationMembershipProvisioner(
    AppDbContext context,
    IClock clock,
    IOptions<RegistrationOptions> options)
    : IRegistrationMembershipProvisioner
{
    private readonly RegistrationOptions _options = options.Value;

    public async Task ProvisionAsync(User user, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);

        var organization = await context.Organizations
            .Include(o => o.Memberships)
            .FirstOrDefaultAsync(
                o => o.Slug == _options.DefaultOrganizationSlug,
                cancellationToken)
            ?? throw new InvalidOperationException(
                $"Kayit organizasyonu bulunamadi: {_options.DefaultOrganizationSlug}.");

        var studentRole = await context.Roles
            .FirstOrDefaultAsync(
                r => r.OrganizationId == null && r.Code == SystemRoles.Student,
                cancellationToken)
            ?? throw new InvalidOperationException("Ogrenci sistem rolu bulunamadi.");

        var membership = organization.AddMember(
            user.Id,
            MembershipStatus.Active,
            clock.UtcNow);

        membership.AssignRole(studentRole.Id);

        // Yeni uyelik ve rol baglantisi ayni SaveChanges icinde kullaniciyla birlikte yazilir.
        context.Add(membership.Roles.Single(r => r.RoleId == studentRole.Id));
    }
}
