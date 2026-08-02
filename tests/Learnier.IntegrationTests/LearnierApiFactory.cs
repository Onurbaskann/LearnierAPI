using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Learnier.IntegrationTests;

/// <summary>
/// Testler icin uygulamayi bellek ici olarak ayaga kaldirir.
/// </summary>
/// <remarks>
/// Yapilandirma acikca burada verilir; testlerin appsettings dosyalarina
/// bagimli olmasi, gelistirme ayari degistiginde testlerin sessizce kaymasina yol acardi.
/// Bu fabrika veritabanina baglanmaz - AppDbContext tembel calisir ve buradaki
/// testler yalnizca HTTP hattini dogrular.
/// </remarks>
public sealed class LearnierApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] =
                    "Host=localhost;Port=5432;Database=learnier_test;Username=test;Password=test",
                ["Jwt:Issuer"] = "learnier-test",
                ["Jwt:Audience"] = "learnier-test-api",
                ["Jwt:SigningKey"] = "integration-test-signing-key-at-least-32-chars",
                ["Jwt:AccessTokenLifetimeMinutes"] = "15"
            });
        });
    }
}
