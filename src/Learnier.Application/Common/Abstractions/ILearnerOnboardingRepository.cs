using Learnier.Domain.Catalog;
using Learnier.Domain.Progress;

namespace Learnier.Application.Common.Abstractions;

public interface ILearnerOnboardingRepository
{
    Task<Subject?> FindSubjectAsync(Guid subjectId, CancellationToken cancellationToken);
    Task<IReadOnlyList<Level>> ListLevelsAsync(Guid subjectId, CancellationToken cancellationToken);
    Task<LearnerOnboardingProfile?> FindAsync(
        Guid learnerUserId,
        Guid subjectId,
        CancellationToken cancellationToken);
    Task<IReadOnlyList<LearnerOnboardingProfile>> ListAsync(
        Guid learnerUserId,
        CancellationToken cancellationToken);
    void Add(LearnerOnboardingProfile profile);
}
