using Learnier.Application.Common.Abstractions;
using Learnier.Domain.Social;
using Microsoft.EntityFrameworkCore;

namespace Learnier.Infrastructure.Persistence.Repositories;

internal sealed class EfClubRepository(AppDbContext context) : IClubRepository
{
    public async Task<Club?> FindByIdAsync(
        Guid clubId,
        bool includeRooms,
        CancellationToken cancellationToken)
    {
        var query = context.Clubs.Include(club => club.Subject).AsQueryable();
        if (includeRooms)
        {
            query = query.Include(club => club.Rooms);
        }

        return await query.FirstOrDefaultAsync(club => club.Id == clubId, cancellationToken);
    }

    public async Task<IReadOnlyList<Club>> ListAsync(
        bool includeInactive,
        CancellationToken cancellationToken)
    {
        var query = context.Clubs
            .AsNoTracking()
            .Include(club => club.Subject)
            .Include(club => club.Rooms)
            .AsQueryable();

        if (!includeInactive)
        {
            query = query.Where(club => club.IsActive);
        }

        return await query.OrderBy(club => club.Name).ToListAsync(cancellationToken);
    }

    public async Task<ClubRoom?> FindRoomAsync(Guid roomId, CancellationToken cancellationToken)
        => await context.ClubRooms
            .Include(room => room.Club)
            .FirstOrDefaultAsync(room => room.Id == roomId, cancellationToken);

    public async Task<IReadOnlyList<ClubMessage>> ListMessagesAsync(
        Guid roomId,
        int limit,
        CancellationToken cancellationToken)
        => await context.ClubMessages
            .AsNoTracking()
            .Include(message => message.AuthorUser)
            .Where(message => message.RoomId == roomId)
            .OrderByDescending(message => message.CreatedAt)
            .Take(limit)
            .OrderBy(message => message.CreatedAt)
            .ToListAsync(cancellationToken);

    public async Task<bool> ExistsForSubjectAsync(
        Guid organizationId,
        Guid subjectId,
        CancellationToken cancellationToken)
        => await context.Clubs.AnyAsync(
            club => club.OrganizationId == organizationId
                    && club.SubjectId == subjectId,
            cancellationToken);

    public void Add(Club club) => context.Clubs.Add(club);

    public void AddMessage(ClubMessage message) => context.ClubMessages.Add(message);
}
