using System.Text;
using Learnier.Application.Common.Abstractions;
using Learnier.Infrastructure.Events;
using Learnier.Infrastructure.Identity;
using Learnier.Infrastructure.Notifications;
using Learnier.Infrastructure.Persistence;
using Learnier.Infrastructure.Persistence.Interceptors;
using Learnier.Infrastructure.Persistence.Queries;
using Learnier.Infrastructure.Persistence.Repositories;
using Learnier.Infrastructure.Persistence.Seeding;
using Learnier.Infrastructure.Time;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

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
        services.AddScoped<IUserRepository, EfUserRepository>();
        services.AddScoped<IRefreshTokenRepository, EfRefreshTokenRepository>();
        services.AddScoped<IEmailVerificationTokenRepository, EfEmailVerificationTokenRepository>();
        services.AddScoped<IOrganizationRepository, EfOrganizationRepository>();
        services.AddScoped<IRoleRepository, EfRoleRepository>();
        services.AddScoped<IMembershipRepository, EfMembershipRepository>();
        services.AddScoped<ICatalogRepository, EfCatalogRepository>();
        services.AddScoped<ICatalogQueries, EfCatalogQueries>();
        services.AddScoped<IInstructorRepository, EfInstructorRepository>();
        services.AddScoped<IInstructorQueries, EfInstructorQueries>();

        // Gercek bir saglayici baglanana kadar e-postalar yalnizca loga yazilir;
        // uretime cikmadan once degistirilmeli (bkz. LoggingEmailSender).
        services.AddSingleton<IEmailSender, LoggingEmailSender>();

        // Tohumlayicilar yalnizca acik "seed" komutunda calisir; kayitli olmalari
        // baslangicta bir sey yapmalari anlamina gelmez.
        services.AddScoped<SystemDataSeeder>();
        services.AddScoped<DevelopmentDataSeeder>();

        services.AddIdentityServices(configuration);

        return services;
    }

    private static IServiceCollection AddIdentityServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .ValidateDataAnnotations()
            // Eksik veya gecersiz JWT ayariyla uygulama hic baslamasin: bu hatanin
            // ilk istekte degil baslangicta ortaya cikmasi cok daha ucuz.
            .ValidateOnStart();

        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddSingleton<IRefreshTokenFactory, RefreshTokenFactory>();
        services.AddSingleton<IEmailVerificationTokenFactory, EmailVerificationTokenFactory>();
        services.AddScoped<ITokenService, JwtTokenService>();

        services.AddScoped<IMembershipProvider, EfMembershipProvider>();
        services.AddScoped<IPermissionProvider, EfPermissionProvider>();
        services.AddScoped<IPermissionCacheInvalidator, PermissionCacheInvalidator>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                var jwt = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
                    ?? throw new InvalidOperationException(
                        $"{JwtOptions.SectionName} bolumu tanimli degil.");

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(jwt.SigningKey)),
                    ValidateLifetime = true,
                    // Varsayilan 5 dakikalik tolerans, kisa omurlu tokenlarda
                    // iptalin etkisini geciktirir; sifirlaniyor.
                    ClockSkew = TimeSpan.Zero
                };
            });

        return services;
    }
}
