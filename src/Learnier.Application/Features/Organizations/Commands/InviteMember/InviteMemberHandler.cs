using Learnier.Application.Common.Abstractions;
using Learnier.Application.Common.Results;
using Learnier.Domain.Identity;

namespace Learnier.Application.Features.Organizations.Commands.InviteMember;

/// <summary>
/// Kayitli bir kullaniciyi aktif organizasyona davet eder.
/// </summary>
/// <remarks>
/// Uyelik <see cref="MembershipStatus.Invited"/> durumunda baslar. Kullanici kabul
/// edene kadar kiraci cozumlemesi bu uyeligi gecerli saymaz, yani davet edilen kisi
/// kurumun verisine erisemez.
/// </remarks>
public sealed class InviteMemberHandler(
    IOrganizationRepository organizations,
    IMembershipRepository memberships,
    IUserRepository users,
    IRoleRepository roles,
    ICurrentTenant currentTenant,
    IUnitOfWork unitOfWork)
{
    public async Task<Result<InviteMemberResult>> Handle(
        InviteMemberCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (currentTenant.OrganizationId is not { } organizationId)
        {
            return OrganizationErrors.OrganizationContextRequired;
        }

        var user = await users.FindByEmailAsync(command.Email, cancellationToken);

        if (user is null)
        {
            return OrganizationErrors.UserNotFound;
        }

        // Rolun bu organizasyonda kullanilabilir olmasi sart: baska bir kurumun
        // ozel rolu buraya atanamaz.
        var role = await roles.FindUsableRoleAsync(command.RoleId, organizationId, cancellationToken);

        if (role is null)
        {
            return OrganizationErrors.RoleNotUsable;
        }

        if (await memberships.ExistsAsync(organizationId, user.Id, cancellationToken))
        {
            return OrganizationErrors.UserAlreadyMember;
        }

        var organization = await organizations.FindWithMembershipsAsync(organizationId, cancellationToken);

        if (organization is null)
        {
            return OrganizationErrors.NotFound;
        }

        var membership = organization.AddMember(user.Id, MembershipStatus.Invited, joinedAt: null);
        membership.AssignRole(role.Id);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        // Onbellek dusurulmuyor: yeni uyeligin henuz onbelleklenmis bir izin
        // kumesi yok, ilk yetkilendirme kontrolunde zaten veritabanindan cozulecek.
        return new InviteMemberResult(membership.Id, user.Id);
    }
}
