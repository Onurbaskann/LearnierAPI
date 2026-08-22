using Learnier.Application.Common.Abstractions;
using Learnier.Domain.Teaching;
using Microsoft.EntityFrameworkCore;

namespace Learnier.Infrastructure.Persistence.Repositories;

/// <inheritdoc cref="IInstructorRepository"/>
/// <remarks>
/// <see cref="InstructorProfile"/> kendi <c>OrganizationId</c> sutununu tasimaz.
/// Kiraci siniri her sorguda uyelik uzerinden acikca korunur: uyelikler kiraci
/// filtresine tabi oldugu icin baska kurumun profili bulunamaz.
/// </remarks>
internal sealed class EfInstructorRepository(AppDbContext context) : IInstructorRepository
{
    public async Task<InstructorProfile?> FindByMembershipAsync(
        Guid membershipId,
        CancellationToken cancellationToken)
        => await context.InstructorProfiles
            .Where(p => p.MembershipId == membershipId)
            .Where(p => context.Memberships.Any(m => m.Id == p.MembershipId))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<InstructorProfile?> FindWithDetailsAsync(
        Guid profileId,
        CancellationToken cancellationToken)
        => await context.InstructorProfiles
            .Include(p => p.Subjects)
            .Include(p => p.Availabilities)
            .Where(p => p.Id == profileId)
            .Where(p => context.Memberships.Any(m => m.Id == p.MembershipId))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<bool> HasOverlappingAvailabilityAsync(
        Guid profileId,
        DayOfWeek dayOfWeek,
        TimeOnly startLocalTime,
        TimeOnly endLocalTime,
        DateOnly validFrom,
        DateOnly? validUntil,
        Guid? excludeAvailabilityId,
        CancellationToken cancellationToken)
    {
        var query = context.InstructorAvailabilities
            .Where(a => a.InstructorProfileId == profileId && a.DayOfWeek == dayOfWeek);

        if (excludeAvailabilityId is { } excludeId)
        {
            query = query.Where(a => a.Id != excludeId);
        }

        return await query.AnyAsync(
            a =>
                // Saat araliklari kesisiyor mu: yeni baslangic mevcut bitisten once
                // VE yeni bitis mevcut baslangictan sonra. Bitisi baslangica esit
                // olan araliklar (13:00-14:00 ve 14:00-15:00) cakisma sayilmaz.
                startLocalTime < a.EndLocalTime
                && endLocalTime > a.StartLocalTime

                // Gecerlilik pencereleri kesisiyor mu: bos ValidUntil "suresiz"
                // demek oldugu icin ayrica ele alinir.
                && (a.ValidUntil == null || validFrom <= a.ValidUntil)
                && (validUntil == null || validUntil >= a.ValidFrom),
            cancellationToken);
    }

    public void AddProfile(InstructorProfile profile) => context.InstructorProfiles.Add(profile);

    public void AddOverride(InstructorAvailabilityOverride availabilityOverride)
        => context.InstructorAvailabilityOverrides.Add(availabilityOverride);
}
