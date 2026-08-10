using Learnier.Domain.Teaching;

namespace Learnier.Application.Common.Abstractions;

/// <summary>
/// Egitmen yazma islemleri.
/// </summary>
/// <remarks>
/// Okuma tarafi <see cref="IInstructorQueries"/>'de; katalogdaki ayrimin aynisi.
/// </remarks>
public interface IInstructorRepository
{
    /// <summary>
    /// Uyelige ait profili bulur.
    /// </summary>
    /// <remarks>
    /// Profil kendi <c>OrganizationId</c> sutununu tasimaz; kiraci siniri uyelik
    /// uzerinden korunur, bu yuzden sorgu uyeligi de kontrol etmelidir.
    /// </remarks>
    Task<InstructorProfile?> FindByMembershipAsync(Guid membershipId, CancellationToken cancellationToken);

    /// <summary>
    /// Profili yetkinlikleri ve uygunluklariyla birlikte getirir.
    /// </summary>
    Task<InstructorProfile?> FindWithDetailsAsync(Guid profileId, CancellationToken cancellationToken);

    /// <summary>
    /// Verilen gunde, verilen gecerlilik araliginda cakisan bir uygunluk var mi?
    /// </summary>
    /// <param name="excludeAvailabilityId">Guncelleme senaryosunda kendisini disla.</param>
    Task<bool> HasOverlappingAvailabilityAsync(
        Guid profileId,
        DayOfWeek dayOfWeek,
        TimeOnly startLocalTime,
        TimeOnly endLocalTime,
        DateOnly validFrom,
        DateOnly? validUntil,
        Guid? excludeAvailabilityId,
        CancellationToken cancellationToken);

    void AddProfile(InstructorProfile profile);

    void AddOverride(InstructorAvailabilityOverride availabilityOverride);
}
