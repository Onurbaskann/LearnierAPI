namespace Learnier.Application.Common.Abstractions;

public sealed record CreditPeriodRenewalResult(
    int ScannedSubscriptions,
    int RenewedPeriods,
    int ExpiredCredits,
    int GrantedCredits,
    int EndedSubscriptions);

/// <summary>
/// Suresi dolan aylik ders haklarini kapatip sonraki donem haklarini uretir.
/// </summary>
public interface ICreditPeriodRenewalProcessor
{
    Task<CreditPeriodRenewalResult> ProcessDueAsync(
        int batchSize,
        CancellationToken cancellationToken);
}
