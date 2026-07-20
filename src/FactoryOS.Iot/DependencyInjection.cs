using FactoryOS.Iot.Registry;
using FactoryOS.Iot.Telemetry;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registration entry point for the FactoryOS <b>IoT Hub</b> — the device registry and telemetry
/// normalization that turn raw device samples into Standard Model meter readings.
/// </summary>
public static class IotServiceCollectionExtensions
{
    /// <summary>Registers the IoT hub services into the dependency-injection container.</summary>
    /// <param name="services">The service collection to add registrations to.</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance, to allow chaining.</returns>
    public static IServiceCollection AddIotHub(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<IDeviceRegistry, InMemoryDeviceRegistry>();
        services.TryAddSingleton<ITelemetryNormalizer, TelemetryNormalizer>();
        services.TryAddSingleton<ITelemetryIngestor, TelemetryIngestor>();

        return services;
    }
}
