using FactoryOS.Contracts.Events;
using FactoryOS.Contracts.StandardModel;
using FactoryOS.Plugins.DigitalTwin;
using FactoryOS.Plugins.DigitalTwin.Domain;
using Microsoft.Extensions.DependencyInjection;

namespace FactoryOS.IntegrationTests.Modules;

/// <summary>
/// The Digital Twin assembled over the real bus: a telemetry reading and an OEE health fact — from two
/// different producers — fold into one asset's live twin, giving its latest gauge, its health and a derived
/// status, with the twin referencing neither producer.
/// </summary>
public sealed class DigitalTwinAssemblyTests
{
    [Fact]
    public async Task Telemetry_and_oee_assemble_one_live_asset_twin()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEventBus();
        services.AddSingleton(new DigitalTwinOptions { DegradedOeeThreshold = 0.60m });
        new DigitalTwinPlugin().ConfigureServices(services);

        var provider = services.BuildServiceProvider();
        var bus = provider.GetRequiredService<IEventBus>();
        var registry = provider.GetRequiredService<IAssetTwinRegistry>();

        var t0 = new DateTimeOffset(2026, 7, 20, 6, 0, 0, TimeSpan.Zero);
        await bus.PublishAsync(new MeterReadingReceived
        {
            Reading = new MeterReading
            {
                Tenant = "acme",
                MeterId = "press-1",
                Metric = "Temperature",
                Value = 47m,
                Unit = "C",
                ReadingAt = t0,
            },
        });
        await bus.PublishAsync(new OeeCalculated
        {
            Tenant = "acme",
            MachineId = "press-1",
            Oee = 0.52m,
            MeetsTarget = false,
            PeriodEnd = t0.AddMinutes(5),
        });

        var twin = registry.Get("acme", "press-1");

        Assert.NotNull(twin);
        var gauge = Assert.Single(twin.Metrics);
        Assert.Equal("Temperature", gauge.Metric);
        Assert.Equal(47m, gauge.Value);
        Assert.NotNull(twin.Health);
        Assert.Equal(0.52m, twin.Health.Value.Oee);
        Assert.Equal(TwinStatus.Degraded, twin.Status); // missed target and below threshold
        Assert.Equal(t0.AddMinutes(5), twin.LastUpdatedAt);
    }
}
