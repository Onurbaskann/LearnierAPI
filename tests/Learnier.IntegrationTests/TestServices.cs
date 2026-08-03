using Learnier.Application.Common.Abstractions;
using Learnier.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Learnier.IntegrationTests;

/// <summary>
/// Infrastructure servislerini test icin ayaga kaldirir.
/// </summary>
/// <remarks>
/// Saglayicilar dogrudan <c>new</c> ile degil DI uzerinden cozulur; boylece
/// <c>AddInfrastructure</c> icindeki kayitlar da testin kapsamina girer.
/// Somut tipler zaten <c>internal</c>, disaridan gorunen tek yuzey arayuzler.
/// </remarks>
internal static class TestServices
{
    public static AsyncServiceScope CreateScope(PostgresFixture postgres)
        => BuildProvider(postgres).CreateAsyncScope();

    /// <summary>
    /// Kok servis saglayicisi. Tohumlayici kendi kapsamini actigi icin ona
    /// kapsam degil saglayicinin kendisi verilir.
    /// </summary>
    public static ServiceProvider BuildProvider(PostgresFixture postgres)
    {
        ArgumentNullException.ThrowIfNull(postgres);

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = postgres.ConnectionString,
                ["Jwt:Issuer"] = "learnier-test",
                ["Jwt:Audience"] = "learnier-test-api",
                ["Jwt:SigningKey"] = "integration-test-signing-key-at-least-32-chars",
                ["Jwt:AccessTokenLifetimeMinutes"] = "15"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();

        // Uygulamada WebApi katmaninda kayitli olan iki bagimlilik.
        // AppDbContext ve izin saglayicisi bunlar olmadan cozulemez.
        services.AddHybridCache();
        services.AddScoped<ICurrentTenant, NoTenant>();
        services.AddScoped<ICurrentUser, AnonymousUser>();

        services.AddInfrastructure(configuration);

        return services.BuildServiceProvider();
    }

    /// <summary>
    /// Organizasyon kapsami disindaki istegi temsil eder.
    /// </summary>
    /// <remarks>
    /// Uyelik ve izin cozumlemesi tam olarak bu noktada calisir: aktif organizasyon
    /// henuz belirlenmemistir, onu belirleyen sorgular test edilmektedir.
    /// </remarks>
    private sealed class NoTenant : ICurrentTenant
    {
        public Guid? OrganizationId => null;

        public Guid? MembershipId => null;

        public bool HasTenant => false;
    }

    /// <summary>
    /// Denetim alanlarini dolduran interceptor'in ihtiyac duydugu kullanici.
    /// Testlerde islem yapan bir kullanici yok.
    /// </summary>
    private sealed class AnonymousUser : ICurrentUser
    {
        public Guid? UserId => null;

        public bool IsAuthenticated => false;
    }
}
