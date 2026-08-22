using Learnier.Application.Common.Abstractions;
using Learnier.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace Learnier.IntegrationTests;

/// <summary>
/// Test suresince yasayan gercek bir PostgreSQL ornegi ayaga kaldirir ve
/// migration'lari uygular.
/// </summary>
/// <remarks>
/// In-memory saglayici bilerek kullanilmiyor: citext, kismi unique index ve
/// check constraint davranislari yalnizca gercek PostgreSQL'de dogrulanabilir.
/// Ayrica EF sorgu cevirisi (LINQ -> SQL) derleme zamaninda degil calisma
/// zamaninda basarisiz olur; bu testlerin asil degeri orada.
/// </remarks>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:18-alpine")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();

        await using var context = CreateContext();
        await context.Database.MigrateAsync();
    }

    public async ValueTask DisposeAsync() => await _container.DisposeAsync();

    /// <summary>
    /// Yeni bir DbContext uretir.
    /// </summary>
    /// <param name="organizationId">
    /// Aktif organizasyon. Verilmezse organizasyon filtresi devre disi kalir -
    /// test verisi hazirlarken istenen davranis budur.
    /// </param>
    public AppDbContext CreateContext(Guid? organizationId = null)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(ConnectionString)
            .UseSnakeCaseNamingConvention()
            .Options;

        return new AppDbContext(options, new StubTenant(organizationId));
    }

    private sealed class StubTenant(Guid? organizationId) : ICurrentTenant
    {
        public Guid? OrganizationId => organizationId;

        public Guid? MembershipId => null;

        public bool HasTenant => organizationId is not null;
    }
}
