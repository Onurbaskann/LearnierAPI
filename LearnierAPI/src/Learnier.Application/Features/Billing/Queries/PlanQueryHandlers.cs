using Learnier.Application.Common.Abstractions;
using Learnier.Application.Common.Results;

namespace Learnier.Application.Features.Billing.Queries;

/// <summary>
/// Kurumun butun planlari - taslak ve emekli olanlar dahil.
/// </summary>
/// <remarks>
/// Yonetim ekrani plani yapilandirmak icin once gormek zorunda; satisa acilmamis
/// plan da listeye girer. Ogrencinin gordugu liste icin
/// <see cref="ListPurchasablePlansHandler"/> kullanilir.
/// </remarks>
public sealed class ListPlansHandler(IPlanQueries queries, ICurrentTenant currentTenant)
{
    public async Task<Result<IReadOnlyList<PlanDetail>>> Handle(
        CancellationToken cancellationToken)
    {
        if (!currentTenant.HasTenant)
        {
            return BillingErrors.OrganizationContextRequired;
        }

        return Result.Success(await queries.ListAsync(cancellationToken));
    }
}

public sealed class GetPlanHandler(IPlanQueries queries, ICurrentTenant currentTenant)
{
    public async Task<Result<PlanDetail>> Handle(
        Guid planId,
        CancellationToken cancellationToken)
    {
        if (!currentTenant.HasTenant)
        {
            return BillingErrors.OrganizationContextRequired;
        }

        // Kiraci filtresi baska kurumun planini zaten eliyor; ayrimi disaridan
        // gorunmesin diye "yok" olarak yanitlanir.
        var plan = await queries.FindAsync(planId, cancellationToken);

        return plan is null ? BillingErrors.PlanNotFound : Result.Success(plan);
    }
}

/// <summary>
/// Ogrencinin satin alabilecegi planlar.
/// </summary>
/// <remarks>
/// Yonetim ucu <c>subscription.manage</c> istedigi icin ogrenci onu kullanamaz.
/// Bu uc izin istemez ama yalnizca satisa acilmis planlari gosterir.
/// </remarks>
public sealed class ListPurchasablePlansHandler(
    IPlanQueries queries,
    ICurrentTenant currentTenant)
{
    public async Task<Result<IReadOnlyList<CatalogPlanItem>>> Handle(
        CancellationToken cancellationToken)
    {
        if (!currentTenant.HasTenant)
        {
            return BillingErrors.OrganizationContextRequired;
        }

        return Result.Success(await queries.ListPurchasableAsync(cancellationToken));
    }
}
