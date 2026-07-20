using FactoryOS.Application.Caching;
using FactoryOS.Application.Files;
using FactoryOS.Application.Localization;
using FactoryOS.Application.Services;
using FactoryOS.Application.Transactions;
using FactoryOS.Infrastructure.Caching;
using FactoryOS.Infrastructure.Configuration;
using FactoryOS.Infrastructure.Execution;
using FactoryOS.Infrastructure.Files;
using FactoryOS.Infrastructure.Identifiers;
using FactoryOS.Infrastructure.Localization;
using FactoryOS.Infrastructure.Serialization;
using FactoryOS.Infrastructure.Time;
using FactoryOS.Infrastructure.Transactions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registers the FactoryOS <b>Infrastructure foundation</b>: the concrete implementations of the Application-layer
/// abstractions (clock, current-context, caching, file storage, localization, transactions) together with the
/// identifier generators and the bindable <see cref="InfrastructureOptions"/>.
/// </summary>
public static class InfrastructureFoundationServiceCollectionExtensions
{
    private static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> EmptyLocalizationCatalog =
        new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Registers the infrastructure foundation services into the dependency-injection container. Uses <c>TryAdd</c>
    /// throughout so a host may override any implementation before or after calling it.
    /// </summary>
    /// <param name="services">The service collection to add registrations to.</param>
    /// <param name="configuration">The application configuration root.</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance, to allow chaining.</returns>
    public static IServiceCollection AddInfrastructureFoundation(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<InfrastructureOptions>()
            .Bind(configuration.GetSection(InfrastructureConstants.ConfigurationSection));

        services.AddMemoryCache();

        // Clock and identifier generators.
        services.TryAddSingleton<IApplicationClock, SystemClock>();
        services.TryAddSingleton<IGuidGenerator, GuidGenerator>();
        services.TryAddScoped<ICorrelationIdAccessor, CorrelationIdAccessor>();

        // Ambient execution/security context and the current-context services that read from it.
        services.TryAddScoped<InfrastructureContext>();
        services.TryAddScoped<ICurrentUser, CurrentUser>();
        services.TryAddScoped<ICurrentTenant, CurrentTenant>();
        services.TryAddScoped<ICurrentFactory, CurrentFactory>();
        services.TryAddScoped<ICurrentPlant, CurrentPlant>();
        services.TryAddScoped<ICurrentWorkCenter, CurrentWorkCenter>();

        // Serialization and caching.
        services.TryAddSingleton<IJsonSerializer, JsonSerializer>();
        services.TryAddSingleton<ICacheKeyGenerator, CacheKeyGenerator>();
        services.TryAddSingleton<ICacheProvider, MemoryCacheProvider>();
        services.TryAddSingleton<ICacheService, CacheService>();

        // File storage over the local file system.
        services.TryAddSingleton<IFileStorage, FileStorage>();
        services.TryAddSingleton<IFileProvider>(provider => provider.GetRequiredService<IFileStorage>());

        // Localization, defaulting to an empty catalog (key-as-fallback) until a host supplies translations.
        services.TryAddSingleton<ILocalizationService>(provider =>
            new LocalizationProvider(
                EmptyLocalizationCatalog,
                provider.GetRequiredService<IOptions<InfrastructureOptions>>()));

        // Transactions over the ambient unit of work.
        services.TryAddScoped<ITransactionManager, TransactionManager>();

        return services;
    }
}
