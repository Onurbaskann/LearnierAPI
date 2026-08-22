using Learnier.Application.Common.Abstractions;
using Learnier.Domain.Catalog;
using Learnier.Domain.Progress;
using Microsoft.EntityFrameworkCore;

namespace Learnier.Infrastructure.Persistence.Repositories;

internal sealed class EfLearnerOnboardingRepository(AppDbContext context)
    : ILearnerOnboardingRepository
{
    public Task<Subject?> FindSubjectAsync(Guid subjectId, CancellationToken cancellationToken)
        => context.Subjects.FirstOrDefaultAsync(subject => subject.Id == subjectId, cancellationToken);

    public async Task<IReadOnlyList<Level>> ListLevelsAsync(
        Guid subjectId,
        CancellationToken cancellationToken)
        => await context.Levels
            .Where(level => level.SubjectId == subjectId)
            .OrderBy(level => level.SortOrder)
            .ToListAsync(cancellationToken);

    public Task<LearnerOnboardingProfile?> FindAsync(
        Guid learnerUserId,
        Guid subjectId,
        CancellationToken cancellationToken)
        => context.LearnerOnboardingProfiles
            .Include(profile => profile.Subject)
            .Include(profile => profile.EstimatedLevel)
            .FirstOrDefaultAsync(
                profile => profile.LearnerUserId == learnerUserId && profile.SubjectId == subjectId,
                cancellationToken);

    public async Task<IReadOnlyList<LearnerOnboardingProfile>> ListAsync(
        Guid learnerUserId,
        CancellationToken cancellationToken)
        => await context.LearnerOnboardingProfiles
            .AsNoTracking()
            .Include(profile => profile.Subject)
            .Include(profile => profile.EstimatedLevel)
            .Where(profile => profile.LearnerUserId == learnerUserId)
            .OrderByDescending(profile => profile.CompletedAt)
            .ToListAsync(cancellationToken);

    public void Add(LearnerOnboardingProfile profile)
        => context.LearnerOnboardingProfiles.Add(profile);
}
