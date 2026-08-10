using Learnier.Application.Common.Abstractions;
using Learnier.Application.Common.Results;
using Learnier.Application.Common.Security;
using Learnier.Domain.Identity;

namespace Learnier.Application.Features.Organizations.Commands.CreateOrganization;

/// <summary>
/// Yeni organizasyon olusturur ve kurucusunu sahip yapar.
/// </summary>
/// <remarks>
/// Kurucunun otomatik olarak sahip rolunu almasi zorunlu: aksi halde yeni kurulan
/// organizasyonu yonetebilecek - ustelik ilk uyeyi davet edebilecek - kimse olmazdi.
/// </remarks>
public sealed class CreateOrganizationHandler(
    IOrganizationRepository organizations,
    IRoleRepository roles,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    public async Task<Result<CreateOrganizationResult>> Handle(
        CreateOrganizationCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (currentUser.UserId is not { } userId)
        {
            return Error.Unauthorized("common.unauthorized");
        }

        var slug = command.Slug.Trim().ToLowerInvariant();

        // On kontrol anlamli bir hata dondurmek icin; yarisi engelleyen asil
        // koruma organizations.slug uzerindeki unique index'tir.
        if (await organizations.SlugExistsAsync(slug, cancellationToken))
        {
            return OrganizationErrors.SlugAlreadyTaken(slug);
        }

        var ownerRole = await roles.FindSystemRoleByCodeAsync(SystemRoles.OrganizationOwner, cancellationToken)
            ?? throw new InvalidOperationException(
                $"'{SystemRoles.OrganizationOwner}' sistem rolu bulunamadi. Referans verisi tohumlanmis mi?");

        var organization = Organization.Create(
            command.Name,
            slug,
            command.OrganizationType,
            command.TimeZoneId,
            command.DefaultCurrency);

        var membership = organization.AddMember(userId, MembershipStatus.Active, clock.UtcNow);
        membership.AssignRole(ownerRole.Id);

        organizations.Add(organization);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new CreateOrganizationResult(organization.Id, organization.Slug, membership.Id);
    }
}
