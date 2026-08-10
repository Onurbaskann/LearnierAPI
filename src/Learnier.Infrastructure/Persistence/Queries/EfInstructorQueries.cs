using Learnier.Application.Common.Abstractions;
using Learnier.Application.Common.Models;
using Learnier.Application.Features.Teaching.Queries;
using Learnier.Domain.Teaching;
using Microsoft.EntityFrameworkCore;

namespace Learnier.Infrastructure.Persistence.Queries;

/// <inheritdoc cref="IInstructorQueries"/>
/// <remarks>
/// Kiraci siniri uyelik uzerinden korunur: profil kendi organizasyon sutununu
/// tasimadigi icin her sorgu uyeliklere baglanir.
/// </remarks>
internal sealed class EfInstructorQueries(AppDbContext context) : IInstructorQueries
{
    public async Task<PagedResult<InstructorListItem>> ListAsync(
        PageRequest page,
        Guid? subjectId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(page);

        var query = context.InstructorProfiles
            .AsNoTracking()
            .Where(p => context.Memberships.Any(m => m.Id == p.MembershipId));

        if (subjectId is { } filterSubjectId)
        {
            query = query.Where(p => p.Subjects.Any(
                s => s.SubjectId == filterSubjectId
                     && s.Status == InstructorSubjectStatus.Active));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            // Ikincil anahtar sayfalar arasi kaymayi engeller.
            .OrderBy(p => p.Membership.User.FirstName)
            .ThenBy(p => p.Id)
            .Skip(page.Skip)
            .Take(page.PageSize)
            .Select(p => new InstructorListItem(
                p.Id,
                p.MembershipId,
                p.Membership.User.FirstName,
                p.Membership.User.LastName,
                p.Status,
                p.TimeZoneId,
                p.Subjects
                    .Where(s => s.Status == InstructorSubjectStatus.Active)
                    .Select(s => s.Subject.Name)
                    .Distinct()
                    .ToList()))
            .ToListAsync(cancellationToken);

        return new PagedResult<InstructorListItem>(items, page.Page, page.PageSize, totalCount);
    }

    public async Task<InstructorDetail?> FindDetailAsync(
        Guid profileId,
        CancellationToken cancellationToken)
        => await context.InstructorProfiles
            .AsNoTracking()
            .Where(p => p.Id == profileId)
            .Where(p => context.Memberships.Any(m => m.Id == p.MembershipId))
            .Select(p => new InstructorDetail(
                p.Id,
                p.MembershipId,
                p.Membership.User.FirstName,
                p.Membership.User.LastName,
                p.Bio,
                p.TimeZoneId,
                p.Status,
                p.DefaultHourlyRate,
                p.DefaultHourlyRateCurrency,
                p.Subjects
                    .Select(s => new InstructorSubjectDetail(
                        s.Id,
                        s.SubjectId,
                        s.Subject.Name,
                        s.LevelId,
                        context.Levels
                            .Where(l => l.Id == s.LevelId)
                            .Select(l => l.Code)
                            .FirstOrDefault(),
                        s.Status))
                    .ToList(),
                p.Availabilities
                    .OrderBy(a => a.DayOfWeek)
                    .ThenBy(a => a.StartLocalTime)
                    .Select(a => new InstructorAvailabilityDetail(
                        a.Id,
                        a.DayOfWeek,
                        a.StartLocalTime,
                        a.EndLocalTime,
                        a.TimeZoneId,
                        a.ValidFrom,
                        a.ValidUntil))
                    .ToList()))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<AvailabilityOverrideDetail>> ListOverridesAsync(
        Guid profileId,
        DateOnly from,
        CancellationToken cancellationToken)
        => await context.InstructorAvailabilityOverrides
            .AsNoTracking()
            .Where(o => o.InstructorProfileId == profileId && o.OverrideDate >= from)
            .Where(o => context.InstructorProfiles.Any(
                p => p.Id == o.InstructorProfileId
                     && context.Memberships.Any(m => m.Id == p.MembershipId)))
            .OrderBy(o => o.OverrideDate)
            .Select(o => new AvailabilityOverrideDetail(
                o.Id,
                o.OverrideDate,
                o.StartLocalTime,
                o.EndLocalTime,
                o.OverrideType,
                o.Reason))
            .ToListAsync(cancellationToken);
}
