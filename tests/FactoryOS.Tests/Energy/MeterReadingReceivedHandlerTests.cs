using FactoryOS.Contracts.Events;
using FactoryOS.Contracts.StandardModel;
using FactoryOS.Plugins.Energy;
using FactoryOS.Plugins.Energy.Application;
using FactoryOS.Plugins.Energy.Domain;

namespace FactoryOS.Tests.Energy;

public sealed class MeterReadingReceivedHandlerTests
{
    private sealed class RecordingEventBus : IEventBus
    {
        public List<IIntegrationEvent> Published { get; } = [];

        public Task PublishAsync<TEvent>(TEvent integrationEvent, EventPublishOptions? options = null, CancellationToken cancellationToken = default)
            where TEvent : IIntegrationEvent
        {
            Published.Add(integrationEvent);
            return Task.CompletedTask;
        }
    }

    private sealed record Harness(MeterReadingReceivedHandler Handler, RecordingEventBus Bus, IEnergyReadModel ReadModel);

    private static Harness Build()
    {
        var options = new EnergyOptions { SpikeThreshold = 0.25m, MinimumSamples = 3, BaselineWindow = 20 };
        var bus = new RecordingEventBus();
        var readModel = new InMemoryEnergyReadModel(options);
        var handler = new MeterReadingReceivedHandler(
            bus, new InMemoryEnergyBaselineStore(options.BaselineWindow), readModel, new InMemoryProcessedEventLog(), options);
        return new Harness(handler, bus, readModel);
    }

    private static MeterReadingReceived Reading(decimal value) => new()
    {
        Reading = new MeterReading
        {
            Tenant = "acme",
            MeterId = "main-incomer",
            Metric = "ActivePower",
            Value = value,
            Unit = "kWh",
            ReadingAt = DateTimeOffset.UnixEpoch,
        },
    };

    private static EventContext Context(MeterReadingReceived e) =>
        new(Guid.NewGuid(), e.EventId, Guid.NewGuid(), null, "trace", EventPriority.Normal, 1, e.OccurredOnUtc);

    private static async Task DeliverAsync(Harness h, MeterReadingReceived e) =>
        await h.Handler.HandleAsync(e, Context(e), CancellationToken.None);

    [Fact]
    public async Task Records_consumption_for_every_reading()
    {
        var h = Build();

        await DeliverAsync(h, Reading(100m));
        await DeliverAsync(h, Reading(100m));

        Assert.Equal(2, h.Bus.Published.OfType<EnergyConsumptionRecorded>().Count());
        var recorded = h.Bus.Published.OfType<EnergyConsumptionRecorded>().First();
        Assert.Equal("acme", recorded.Tenant);
        Assert.Equal("kWh", recorded.Unit);
    }

    [Fact]
    public async Task Emits_a_spike_once_the_baseline_is_established_and_exceeded()
    {
        var h = Build();

        // Four steady readings establish a baseline of 100 (no spike yet).
        for (var i = 0; i < 4; i++)
        {
            await DeliverAsync(h, Reading(100m));
        }

        Assert.Empty(h.Bus.Published.OfType<EnergySpikeDetected>());

        // A reading double the baseline trips the spike.
        await DeliverAsync(h, Reading(200m));

        var spike = Assert.Single(h.Bus.Published.OfType<EnergySpikeDetected>());
        Assert.Equal(100m, spike.Baseline);
        Assert.Equal(100m, spike.DeltaPercent);
        Assert.Equal(200m, spike.Value);
    }

    [Fact]
    public async Task The_read_model_tracks_the_latest_reading_and_baseline_per_meter()
    {
        var h = Build();

        await DeliverAsync(h, Reading(100m));
        await DeliverAsync(h, Reading(120m));

        var meter = Assert.Single(h.ReadModel.Meters("acme"));
        Assert.Equal("main-incomer", meter.MeterId);
        Assert.Equal(120m, meter.Value); // latest wins
        Assert.Equal("kWh", meter.Unit);
    }

    [Fact]
    public async Task A_detected_spike_lands_on_the_read_models_feed()
    {
        var h = Build();

        for (var i = 0; i < 4; i++)
        {
            await DeliverAsync(h, Reading(100m));
        }
        Assert.Empty(h.ReadModel.Spikes("acme", 10));

        await DeliverAsync(h, Reading(200m));

        var spike = Assert.Single(h.ReadModel.Spikes("acme", 10));
        Assert.Equal(200m, spike.Value);
        Assert.Equal(100m, spike.Baseline);

        var summary = h.ReadModel.Summarize("acme");
        Assert.Equal(1, summary.Meters);
        Assert.Equal(1, summary.Spikes);
    }

    [Fact]
    public async Task Redelivery_of_the_same_event_is_ignored()
    {
        var h = Build();
        var reading = Reading(100m);

        await DeliverAsync(h, reading);
        await DeliverAsync(h, reading); // same EventId — at-least-once duplicate

        Assert.Single(h.Bus.Published.OfType<EnergyConsumptionRecorded>()); // counted once
    }

    [Fact]
    public async Task A_duplicate_does_not_pollute_the_baseline()
    {
        var h = Build();

        // Establish baseline of 100 over three distinct readings, replaying each once as a duplicate.
        for (var i = 0; i < 3; i++)
        {
            var r = Reading(100m);
            await DeliverAsync(h, r);
            await DeliverAsync(h, r); // duplicate must not fold 100 in twice
        }

        // Baseline is a clean 100; a 130 reading (+30%) is a spike. Had duplicates polluted the window it
        // would still be 100, but had they been *counted*, the sample count/average logic would differ.
        await DeliverAsync(h, Reading(130m));

        var spike = Assert.Single(h.Bus.Published.OfType<EnergySpikeDetected>());
        Assert.Equal(100m, spike.Baseline);
        Assert.Equal(30m, spike.DeltaPercent);
    }
}
