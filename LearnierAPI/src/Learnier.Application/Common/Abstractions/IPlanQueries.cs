using Learnier.Domain.Billing;
using Learnier.Domain.Scheduling;

namespace Learnier.Application.Common.Abstractions;

/// <param name="ValidUntil">Arsivlenen fiyatta doludur; aktif fiyatta bostur.</param>
public sealed record PlanPriceItem(
    Guid Id,
    string Currency,
    decimal Amount,
    BillingInterval BillingInterval,
    int BillingIntervalCount,
    PlanPriceStatus Status,
    DateTimeOffset ValidFrom,
    DateTimeOffset? ValidUntil);

/// <param name="Quantity">Bos ise sinirsiz.</param>
/// <param name="LessonDurationMinutes">Yalnizca birebir ders kredisinde dolu.</param>
public sealed record PlanEntitlementItem(
    Guid Id,
    EntitlementType EntitlementType,
    SessionType SessionType,
    int? Quantity,
    EntitlementResetPeriod ResetPeriod,
    int? LessonDurationMinutes);

public sealed record PlanAccessItem(Guid Id, string Name);

/// <summary>
/// Yonetim ekraninin gordugu plan.
/// </summary>
/// <param name="ActivePrice">
/// Satista kullanilan fiyat. Taslak planlarda ve yalnizca arsiv fiyati kalmis
/// planlarda bostur.
/// </param>
/// <param name="Prices">Arsivlenenler dahil butun fiyat surumleri.</param>
/// <param name="IsSystemGenerated">
/// Satin alma akisinin ortuk urettigi plan. Yonetim listesinde gorunur ama
/// ogrenci kataloguna girmez.
/// </param>
public sealed record PlanDetail(
    Guid Id,
    string Name,
    string? Description,
    CatalogAccess CatalogAccess,
    PlanStatus Status,
    bool IsSystemGenerated,
    PlanPriceItem? ActivePrice,
    IReadOnlyList<PlanPriceItem> Prices,
    IReadOnlyList<PlanEntitlementItem> Entitlements,
    IReadOnlyList<PlanAccessItem> SubjectAccess,
    IReadOnlyList<PlanAccessItem> CourseAccess,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

/// <summary>
/// Ogrencinin satin alabilecegi plan.
/// </summary>
/// <remarks>
/// Yonetim goruntusunden ayri tutulur: taslak durumu, arsiv fiyatlari ve
/// sistem uretimi bayragi satin alma kararina girmez.
/// </remarks>
public sealed record CatalogPlanItem(
    Guid Id,
    string Name,
    string? Description,
    CatalogAccess CatalogAccess,
    PlanPriceItem ActivePrice,
    IReadOnlyList<PlanEntitlementItem> Entitlements,
    IReadOnlyList<PlanAccessItem> SubjectAccess,
    IReadOnlyList<PlanAccessItem> CourseAccess);

/// <summary>
/// Plan okuma sorgulari.
/// </summary>
/// <remarks>
/// Butun sorgular kiraci filtresine tabidir: baska kurumun plani hicbir uctan
/// donmez.
/// </remarks>
public interface IPlanQueries
{
    Task<IReadOnlyList<PlanDetail>> ListAsync(CancellationToken cancellationToken);

    Task<PlanDetail?> FindAsync(Guid planId, CancellationToken cancellationToken);

    /// <summary>Ogrencinin satin alabilecegi aktif planlar.</summary>
    Task<IReadOnlyList<CatalogPlanItem>> ListPurchasableAsync(CancellationToken cancellationToken);
}
