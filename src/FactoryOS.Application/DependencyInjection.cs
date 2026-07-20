using FactoryOS.Application.Behaviors;
using FactoryOS.Application.Configuration;
using FactoryOS.Application.Messaging;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registration entry point for the FactoryOS <b>Application</b> layer (use cases, CQRS
/// handlers and application-facing abstractions).
/// </summary>
public static class ApplicationServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Application layer services into the dependency-injection container: the scoped request context,
    /// tunable options and the open-generic pipeline behaviors (logging, validation, performance, transaction,
    /// authorization) in the order they should wrap a handler.
    /// </summary>
    /// <param name="services">The service collection to add registrations to.</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance, to allow chaining.</returns>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<ApplicationOptions>();

        services.AddScoped<ApplicationContext>();
        services.AddScoped<IRequestContext>(provider => provider.GetRequiredService<ApplicationContext>());

        // Behaviors compose in registration order: the first added is the outermost wrapper.
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(PerformanceBehavior<,>));
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(AuthorizationBehavior<,>));
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(TransactionBehavior<,>));

        return services;
    }
}
