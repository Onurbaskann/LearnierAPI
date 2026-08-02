using System.Reflection;
using FluentValidation;
using Learnier.Application.Common.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Learnier.Application;

public static class DependencyInjection
{
    /// <summary>
    /// Bu assembly'deki handler'lari ve validator'lari DI'a kaydeder.
    /// </summary>
    /// <remarks>
    /// Mediator kutuphanesi yerine Scrutor'un assembly taramasi kullaniliyor.
    /// Konvansiyon: use-case handler'lari <c>Handler</c> son ekiyle biter ve kendi
    /// somut tipleriyle kaydedilir. Controller onlari <c>[FromServices]</c> ile
    /// dogrudan alir; boylece bagimlilik gizlenmez ve derleme zamaninda dogrulanir.
    /// </remarks>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        var assembly = Assembly.GetExecutingAssembly();

        services.Scan(scan => scan
            .FromAssemblies(assembly)
            .AddClasses(classes => classes.Where(type =>
                type.Name.EndsWith("Handler", StringComparison.Ordinal)
                && !type.IsAbstract), publicOnly: false)
            .AsSelf()
            .WithScopedLifetime());

        // Domain olayi handler'lari arayuzleriyle kaydedilir: bir olayin
        // birden fazla handler'i olabilir ve dispatcher hepsini cozer.
        services.Scan(scan => scan
            .FromAssemblies(assembly)
            .AddClasses(classes => classes.AssignableTo(typeof(IDomainEventHandler<>)), publicOnly: false)
            .AsImplementedInterfaces()
            .WithScopedLifetime());

        services.AddValidatorsFromAssembly(assembly, includeInternalTypes: true);

        return services;
    }
}
