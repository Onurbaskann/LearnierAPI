using System.ComponentModel.DataAnnotations;

namespace Learnier.Infrastructure.Scheduling;

internal sealed class SessionCompletionOptions
{
    public const string SectionName = "SessionCompletion";

    public bool Enabled { get; init; } = true;

    [Range(1, 1440)]
    public int IntervalMinutes { get; init; } = 10;

    [Range(1, 1000)]
    public int BatchSize { get; init; } = 100;

    /// <summary>
    /// Ders bittikten sonra otomatik tamamlamaya kadar beklenen sure.
    /// </summary>
    /// <remarks>
    /// Egitmen gercek yoklamayi girecekse otomatik islemle yarismasin diye kisa
    /// bir pencere birakilir. Onay beklemez; yalnizca geciktirir.
    /// </remarks>
    [Range(0, 1440)]
    public int GracePeriodMinutes { get; init; } = 15;
}
