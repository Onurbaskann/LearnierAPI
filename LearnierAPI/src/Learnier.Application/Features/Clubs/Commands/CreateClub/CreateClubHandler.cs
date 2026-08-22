using Learnier.Application.Common.Abstractions;
using Learnier.Application.Common.Results;
using Learnier.Domain.Social;

namespace Learnier.Application.Features.Clubs.Commands.CreateClub;

/// <summary>
/// Aktif organizasyonda bir ders alanina bagli kulup olusturur.
/// </summary>
public sealed class CreateClubHandler(
    IClubRepository clubs,
    ICatalogRepository catalog,
    ICurrentTenant currentTenant,
    IUnitOfWork unitOfWork)
{
    public async Task<Result<CreateClubResult>> Handle(
        CreateClubCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (currentTenant.OrganizationId is not { } organizationId)
        {
            return ClubErrors.OrganizationContextRequired;
        }

        // Subject sorgusu tenant filtresine tabi oldugu icin baska kurumun
        // ders alani kimligi verilse bile bulunamaz.
        var subject = await catalog.FindSubjectAsync(command.SubjectId, cancellationToken);

        if (subject is null)
        {
            return ClubErrors.SubjectNotFound;
        }

        if (await clubs.ExistsForSubjectAsync(
                organizationId,
                subject.Id,
                cancellationToken))
        {
            return ClubErrors.AlreadyExistsForSubject;
        }

        var club = Club.Create(
            organizationId,
            subject.Id,
            command.Name,
            command.Description);

        club.AddRoom("genel-sohbet", ClubRoomType.Text, 0);

        clubs.Add(club);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new CreateClubResult(club.Id);
    }
}
