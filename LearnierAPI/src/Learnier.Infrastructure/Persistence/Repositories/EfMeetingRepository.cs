using Learnier.Application.Common.Abstractions;
using Learnier.Domain.Scheduling;
using Microsoft.EntityFrameworkCore;

namespace Learnier.Infrastructure.Persistence.Repositories;

internal sealed class EfMeetingRepository(AppDbContext context) : IMeetingRepository
{
    public Task<Meeting?> FindBySessionAsync(
        Guid sessionId,
        CancellationToken cancellationToken)
        => context.Meetings.SingleOrDefaultAsync(
            meeting => meeting.SessionId == sessionId,
            cancellationToken);

    public void Add(Meeting meeting) => context.Meetings.Add(meeting);
}
