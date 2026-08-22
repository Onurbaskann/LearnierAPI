using Learnier.Domain.Billing;

namespace Learnier.Application.Common.Abstractions;

/// <summary>
/// Abonelik plani yazma islemleri - yonetici tarafi.
/// </summary>
/// <remarks>
/// Ogrencinin satin alma akisi <see cref="IPackagePurchaseRepository"/> uzerinden
/// yurur ve plani adiyla arar; burasi plani kimligiyle bulur, cunku yonetici
/// zaten olusturdugu planin kimligini elinde tutar. Ikisini ayirmak, satin alma
/// yolunun yonetim metotlariyla sismesini engeller.
/// </remarks>
public interface IPlanRepository
{
    /// <summary>
    /// Plani bulur. Kiraci filtresi geregi baska kurumun plani donmez.
    /// </summary>
    /// <param name="includeDetails">
    /// Fiyat ve hak tanimlarini birlikte getirir. Aktiflestirme ve fiyat ekleme
    /// bunlara bakar; erisim tanimlarken gereksizdir.
    /// </param>
    Task<SubscriptionPlan?> FindPlanAsync(
        Guid planId,
        bool includeDetails,
        CancellationToken cancellationToken);

    void AddPlan(SubscriptionPlan plan);

    void AddSubjectAccess(PlanSubjectAccess access);

    void AddCourseAccess(PlanCourseAccess access);
}
