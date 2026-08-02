using Learnier.Application.Common.Abstractions;
using Learnier.Infrastructure.Events;
using Learnier.Infrastructure.Persistence;
using Learnier.Infrastructure.Persistence.Interceptors;
using Learnier.Infrastructure.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Learnier.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException(
                "ConnectionStrings:Default tanimli degil. .env veya appsettings uzerinden saglayin.");

        services.AddSingleton<IClock, SystemClock>();
        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();

        services.AddScoped<AuditableEntityInterceptor>();
        services.AddScoped<DomainEventDispatchInterceptor>();

        services.AddDbContext<AppDbContext>((serviceProvider, options) =>
        {
            options.UseNpgsql(connectionString, npgsql =>
            {
                // Migration'lar Infrastructure assembly'sinde tutulur.
                npgsql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName);

                // EnableRetryOnFailure bilincli olarak KAPALI.
                // Yeniden deneyen execution strategy, elle baslatilan transaction'lari
                // reddeder ("does not support user-initiated transactions"). Rezervasyon
                // akisi kontenjan yarisini onlemek icin acik transaction ve satir kilidi
                // kullaniyor; retry acilirsa o akis calisma zamaninda kirilir.
                // Ileride retry gerekirse transaction'lar execution strategy'nin
                // ExecuteAsync sarmalayicisi icine alinmali.
            });

            // C# tarafinda PascalCase yazilan isimler veritabaninda snake_case olur.
            // Boylece kaynak dokumandaki tablo/kolon adlari elle eslestirilmeden ortaya cikar.
            options.UseSnakeCaseNamingConvention();

            options.AddInterceptors(
                serviceProvider.GetRequiredService<AuditableEntityInterceptor>(),
                serviceProvider.GetRequiredService<DomainEventDispatchInterceptor>());
        });

        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<AppDbContext>());

        return services;
    }
}
