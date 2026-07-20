using FactoryOS.Contracts.Events;
using FactoryOS.Contracts.StandardModel;
using FactoryOS.Plugins.DigitalTwin;
using FactoryOS.Plugins.DigitalTwin.Application;
using FactoryOS.Plugins.DigitalTwin.Domain;

namespace FactoryOS.Tests.DigitalTwin;

public sealed class DigitalTwinHandlerTests
{
    private static readonly DateTimeOffset T0 = new(2026, 7, 20, 0, 0, 0, TimeSpan.Zero);

    private sealed record Harness(IAssetTwinRegistry Registry, IProcessedEventLog Processed);

    private static Harness Build() =>
        new(new InMemoryAssetTwinRegistry(new DigitalTwinOptions()), new InMemoryProcessedEventLog());

    private static EventContext Context(IIntegrationEvent e) =>
        new(Guid.NewGuid(), e.EventId, Guid.NewGuid(), null, "trace", EventPriority.Normal, 1, e.OccurredOnUtc);

    private static MeterReadingReceived Telemetry(Guid? id = null, decimal value = 42m) => new()
    {
        EventId = id ?? Guid.NewGuid(),
        Reading = new MeterReading
        {
            Tenant = "acme",
            MeterId = "press-1",
            Metric = "Temperature",
            Value = value,
            Unit = "C",
            ReadingAt = T0,
        },
    };

    [Fact]
    public async Task Telemetry_is_mirrored_onto_the_asset_twin()
    {
        var h = Build();
        var handler = new MeterReadingReceivedHandler(h.Registry, h.Processed);
        var evt = Telemetry();

        await handler.HandleAsync(evt, Context(evt), CancellationToken.None);

        var twin = h.Registry.Get("acme", "press-1");
        var reading = Assert.Single(twin!.Metrics);
        Assert.Equal("Temperature", reading.Metric);
        Assert.Equal(42m, reading.Value);
    }

    [Fact]
    public async Task Redelivered_telemetry_is_folded_once()
    {
        var h = Build();
        var handler = new MeterReadingReceivedHandler(h.Registry, h.Processed);
        var evt = Telemetry(value: 42m);

        await handler.HandleAsync(evt, Context(evt), CancellationToken.None);
        await handler.HandleAsync(evt, Context(evt), CancellationToken.None); // same event id

        Assert.Single(h.Registry.Get("acme", "press-1")!.Metrics);
    }

    [Fact]
    public async Task Oee_is_mirrored_as_asset_health()
    {
        var h = Build();
        var handler = new OeeCalculatedHandler(h.Registry, h.Processed);
        var evt = new OeeCalculated
        {
            EventId = Guid.NewGuid(),
            Tenant = "acme",
            MachineId = "press-1",
            Oee = 0.9m,
            MeetsTarget = true,
            PeriodEnd = T0,
        };

        await handler.HandleAsync(evt, Context(evt), CancellationToken.None);

        var twin = h.Registry.Get("acme", "press-1");
        Assert.NotNull(twin!.Health);
        Assert.Equal(0.9m, twin.Health.Value.Oee);
        Assert.True(twin.Health.Value.MeetsTarget);
    }
}
