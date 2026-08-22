using Microsoft.Extensions.DependencyInjection;

namespace Learnier.Infrastructure.Persistence.Seeding;

/// <summary>
/// Tohumlamanin disaridan cagrilan tek giris noktasi.
/// </summary>
/// <remarks>
/// Somut tohumlayicilar <c>internal</c>; WebApi katmani onlari dogrudan degil bu
/// yuzey uzerinden calistirir. Tohumlama uygulama baslangicinda otomatik yapilmaz -
/// migration'da oldugu gibi acik bir adimdir, cunku hangi verinin ne zaman yazildigi
/// tahmin edilebilir olmali.
/// </remarks>
public static class DatabaseSeeder
{
    /// <param name="includeDevelopmentData">
    /// Ornek kurum ve test hesaplarinin da olusturulup olusturulmayacagi.
    /// Yalnizca gelistirme ortaminda dogru verilmelidir.
    /// </param>
    public static async Task RunAsync(
        IServiceProvider services,
        bool includeDevelopmentData,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(services);

        await using var scope = services.CreateAsyncScope();

        await scope.ServiceProvider
            .GetRequiredService<SystemDataSeeder>()
            .SeedAsync(cancellationToken);

        if (!includeDevelopmentData)
        {
            return;
        }

        await scope.ServiceProvider
            .GetRequiredService<DevelopmentDataSeeder>()
            .SeedAsync(cancellationToken);
    }
}
