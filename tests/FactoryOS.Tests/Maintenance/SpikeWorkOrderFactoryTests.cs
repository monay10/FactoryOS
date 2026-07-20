using FactoryOS.Contracts.Events;
using FactoryOS.Plugins.Maintenance;
using FactoryOS.Plugins.Maintenance.Domain;

namespace FactoryOS.Tests.Maintenance;

public sealed class SpikeWorkOrderFactoryTests
{
    private static readonly MaintenanceOptions Options = new() { SpikeWorkOrderDueInHours = 24, SpikeWorkOrderPrefix = "WO" };

    private static EnergySpikeDetected Spike(Guid eventId) => new()
    {
        EventId = eventId,
        Tenant = "acme",
        MeterId = "main-incomer",
        Metric = "ActivePower",
        Value = 250m,
        Baseline = 100m,
        DeltaPercent = 150m,
        Unit = "kWh",
        ReadingAt = DateTimeOffset.UnixEpoch,
    };

    [Fact]
    public void Numbers_the_work_order_deterministically_from_the_event_id()
    {
        var spike = Spike(new Guid("12345678-90ab-cdef-1234-567890abcdef"));

        var first = SpikeWorkOrderFactory.FromSpike(spike, Options);
        var second = SpikeWorkOrderFactory.FromSpike(spike, Options);

        Assert.Equal("WO-12345678", first.Number);
        Assert.Equal(first.Number, second.Number); // same spike → same number (idempotency basis)
    }

    [Fact]
    public void Builds_an_open_work_order_targeting_the_meter()
    {
        var wo = SpikeWorkOrderFactory.FromSpike(Spike(Guid.NewGuid()), Options);

        Assert.Equal("acme", wo.Tenant);
        Assert.Equal("Open", wo.Status);
        Assert.Equal("main-incomer", wo.AssetCode);
        Assert.Equal(DateTimeOffset.UnixEpoch.AddHours(24), wo.DueAt);
        Assert.Contains("main-incomer", wo.Title, StringComparison.Ordinal);
        Assert.Contains("150", wo.Title, StringComparison.Ordinal);
    }
}
