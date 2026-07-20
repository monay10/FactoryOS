using FactoryOS.Plugins.DigitalTwin;
using FactoryOS.Plugins.DigitalTwin.Domain;

namespace FactoryOS.Tests.DigitalTwin;

public sealed class InMemoryAssetTwinRegistryTests
{
    private static InMemoryAssetTwinRegistry Registry(decimal threshold = 0.60m) =>
        new(new DigitalTwinOptions { DegradedOeeThreshold = threshold });

    private static readonly DateTimeOffset T0 = new(2026, 7, 20, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void An_unknown_asset_has_no_twin()
    {
        Assert.Null(Registry().Get("acme", "press-1"));
        Assert.Empty(Registry().Assets("acme"));
    }

    [Fact]
    public void Metrics_are_mirrored_latest_value_per_metric_and_ordered()
    {
        var registry = Registry();
        registry.RecordMetric("acme", "press-1", new MetricReading("Temperature", 40m, "C", T0));
        registry.RecordMetric("acme", "press-1", new MetricReading("ActivePower", 12m, "kW", T0));
        registry.RecordMetric("acme", "press-1", new MetricReading("Temperature", 45m, "C", T0.AddMinutes(1)));

        var twin = registry.Get("acme", "press-1");

        Assert.NotNull(twin);
        Assert.Equal(2, twin.Metrics.Count);
        Assert.Equal("ActivePower", twin.Metrics[0].Metric); // ordered by name
        Assert.Equal("Temperature", twin.Metrics[1].Metric);
        Assert.Equal(45m, twin.Metrics[1].Value); // latest value won
        Assert.Equal(T0.AddMinutes(1), twin.LastUpdatedAt);
        Assert.Equal(TwinStatus.Online, twin.Status);
    }

    [Fact]
    public void An_out_of_order_metric_is_ignored()
    {
        var registry = Registry();
        registry.RecordMetric("acme", "press-1", new MetricReading("Temperature", 45m, "C", T0.AddMinutes(5)));
        registry.RecordMetric("acme", "press-1", new MetricReading("Temperature", 40m, "C", T0)); // older

        var twin = registry.Get("acme", "press-1");

        Assert.Equal(45m, Assert.Single(twin!.Metrics).Value);
        Assert.Equal(T0.AddMinutes(5), twin.LastUpdatedAt);
    }

    [Fact]
    public void Health_below_threshold_and_missing_target_reads_as_degraded()
    {
        var registry = Registry(threshold: 0.60m);
        registry.RecordHealth("acme", "press-1", new AssetHealth(0.55m, MeetsTarget: false, T0));

        Assert.Equal(TwinStatus.Degraded, registry.Get("acme", "press-1")!.Status);
    }

    [Fact]
    public void Health_meeting_target_reads_as_online_even_if_low()
    {
        var registry = Registry(threshold: 0.60m);
        registry.RecordHealth("acme", "press-1", new AssetHealth(0.55m, MeetsTarget: true, T0));

        Assert.Equal(TwinStatus.Online, registry.Get("acme", "press-1")!.Status);
    }

    [Fact]
    public void Assets_and_tenants_are_isolated_and_listed()
    {
        var registry = Registry();
        registry.RecordMetric("acme", "press-2", new MetricReading("Temperature", 40m, "C", T0));
        registry.RecordMetric("acme", "press-1", new MetricReading("Temperature", 40m, "C", T0));

        Assert.Equal(["press-1", "press-2"], registry.Assets("acme"));
        Assert.Empty(registry.Assets("globex"));
        Assert.Null(registry.Get("globex", "press-1"));
    }
}
