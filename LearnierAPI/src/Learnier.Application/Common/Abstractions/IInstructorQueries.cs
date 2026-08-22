using Learnier.Application.Common.Models;
using Learnier.Application.Features.Teaching.Queries;

namespace Learnier.Application.Common.Abstractions;

/// <summary>
/// Egitmen okuma sorgulari.
/// </summary>
/// <remarks>
/// Sorgular aktif organizasyonun uyelikleri uzerinden filtrelenir; baska kurumun
/// egitmenleri donmez.
/// </remarks>
public interface IInstructorQueries
{
    /// <param name="subjectId">Verilirse yalnizca o alanda yetkin egitmenler doner.</param>
    Task<PagedResult<InstructorListItem>> ListAsync(
        PageRequest page,
        Guid? subjectId,
        CancellationToken cancellationToken);

    Task<InstructorDetail?> FindDetailAsync(Guid profileId, CancellationToken cancellationToken);

    /// <param name="from">Bu tarihten itibaren gecerli istisnalar.</param>
    Task<IReadOnlyList<AvailabilityOverrideDetail>> ListOverridesAsync(
        Guid profileId,
        DateOnly from,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<InstructorStudentListItem>?> ListMyStudentsAsync(
        Guid membershipId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<InstructorScheduleListItem>?> ListMyScheduleAsync(
        Guid membershipId,
        DateTimeOffset? from,
        DateTimeOffset? until,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    Task<InstructorDashboardStats?> FindMyDashboardAsync(
        Guid membershipId,
        DateTimeOffset monthStartsAt,
        DateTimeOffset monthEndsAt,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<InstructorEarningListItem>?> ListMyEarningsAsync(
        Guid membershipId,
        DateTimeOffset? from,
        DateTimeOffset? until,
        CancellationToken cancellationToken);
}
