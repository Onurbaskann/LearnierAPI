using Learnier.Application.Common.Abstractions;
using Learnier.Infrastructure.Persistence;
using Learnier.Infrastructure.Persistence.Seeding;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace Learnier.IntegrationTests;

/// <summary>
/// Uygulamayi gercek bir PostgreSQL uzerinde, migration'lari uygulanmis ve
/// tohumlanmis halde ayaga kaldirir.
/// </summary>
/// <remarks>
/// <see cref="LearnierApiFactory"/> veritabanina baglanmaz ve yalnizca HTTP hattini
/// dogrular. Giris akisi ise kullanici, uyelik ve rol kayitlarina dayandigi icin
/// gercek veri olmadan anlamli test edilemez.
/// </remarks>
public sealed class AuthApiFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:18-alpine")
        .Build();

    private WebApplicationFactory<Program>? _factory;

    public HttpClient CreateClient()
        => _factory?.CreateClient()
           ?? throw new InvalidOperationException("Fixture henuz baslatilmadi.");

    /// <summary>
    /// Ayni veritabanina dogrudan baglanan bir context uretir.
    /// </summary>
    /// <remarks>
    /// HTTP ile gorulemeyen kayitlari hazirlamak veya dogrulamak icin. Ornegin
    /// e-posta dogrulama tokeninin ham hali yalnizca gondericiye verilir; testin
    /// akisi tamamlayabilmesi icin kaydi kendisi yazmasi gerekir.
    /// </remarks>
    public AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_container.GetConnectionString())
            .UseSnakeCaseNamingConvention()
            .Options;

        return new AppDbContext(options, new NoTenant());
    }

    /// <summary>Organizasyon kapsami disinda calisir; kiraci filtresi devre disi kalir.</summary>
    private sealed class NoTenant : ICurrentTenant
    {
        public Guid? OrganizationId => null;

        public Guid? MembershipId => null;

        public bool HasTenant => false;
    }

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();

        _factory = new DatabaseBackedFactory(_container.GetConnectionString());

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await context.Database.MigrateAsync();
        }

        // Ornek hesaplar dahil tohumlanir: giris testleri tam da o hesaplarla calisir.
        await DatabaseSeeder.RunAsync(_factory.Services, includeDevelopmentData: true);
    }

    public async ValueTask DisposeAsync()
    {
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }

        await _container.DisposeAsync();
    }

    private sealed class DatabaseBackedFactory(string connectionString) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            // Ornek hesaplarin tohumlanabilmesi icin Development gerekli.
            builder.UseEnvironment("Development");

            // Degerler UseSetting ile host yapilandirmasina yazilir.
            // ConfigureAppConfiguration yeterli degil: appsettings.Development.json
            // yerel PostgreSQL'i isaret ediyor ve o kaynak sonradan yuklendigi icin
            // testin baglanti dizesini eziyordu - testler container yerine
            // gelistirme veritabanina baglanirdi.
            builder.UseSetting("ConnectionStrings:Default", connectionString);
            builder.UseSetting("Jwt:Issuer", "learnier-test");
            builder.UseSetting("Jwt:Audience", "learnier-test-api");
            builder.UseSetting("Jwt:SigningKey", "integration-test-signing-key-at-least-32-chars");
            builder.UseSetting("Jwt:AccessTokenLifetimeMinutes", "15");
            builder.UseSetting("CreditRenewal:Enabled", "false");
        }
    }
}
