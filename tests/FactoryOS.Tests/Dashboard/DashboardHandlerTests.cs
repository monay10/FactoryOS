using FactoryOS.Contracts.Events;
using FactoryOS.Plugins.Dashboard;
using FactoryOS.Plugins.Dashboard.Application;
using FactoryOS.Plugins.Dashboard.Domain;

namespace FactoryOS.Tests.Dashboard;

public sealed class DashboardHandlerTests
{
    private sealed record Harness(IOperationsBoard Board, IProcessedEventLog Processed);

    private static Harness Build()
    {
        var board = new InMemoryOperationsBoard(new DashboardOptions());
        return new Harness(board, new InMemoryProcessedEventLog());
    }

    private static EventContext Context(IIntegrationEvent e) =>
        new(Guid.NewGuid(), e.EventId, Guid.NewGuid(), null, "trace", EventPriority.Normal, 1, e.OccurredOnUtc);

    [Fact]
    public async Task Oee_is_folded_into_a_machine_tile()
    {
        var h = Build();
        var handler = new OeeCalculatedHandler(h.Board, h.Processed);
        var evt = new OeeCalculated
        {
            Tenant = "acme",
            MachineId = "m-1",
            Oee = 0.82m,
            MeetsTarget = true,
            PeriodEnd = DateTimeOffset.UnixEpoch,
        };

        await handler.HandleAsync(evt, Context(evt), CancellationToken.None);

        var tile = Assert.Single(h.Board.Snapshot("acme").Machines);
        Assert.Equal("m-1", tile.MachineId);
        Assert.Equal(0.82m, tile.Oee);
        Assert.True(tile.MeetsTarget);
    }

    [Fact]
    public async Task A_safety_stand_down_becomes_a_critical_alert()
    {
        var h = Build();
        var handler = new SafetyStandDownTriggeredHandler(h.Board, h.Processed);
        var evt = new SafetyStandDownTriggered
        {
            Tenant = "acme",
            SiteId = "site-1",
            Reason = "HighSeverity",
            TriggerSeverity = 5,
            OccurredAt = DateTimeOffset.UnixEpoch,
        };

        await handler.HandleAsync(evt, Context(evt), CancellationToken.None);

        var alert = Assert.Single(h.Board.Snapshot("acme").RecentAlerts);
        Assert.Equal(AlertLevels.Critical, alert.Level);
        Assert.Contains("site-1", alert.Subject, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_low_stock_alert_becomes_a_warning()
    {
        var h = Build();
        var handler = new LowStockDetectedHandler(h.Board, h.Processed);
        var evt = new LowStockDetected
        {
            Tenant = "acme",
            WarehouseId = "wh-1",
            Sku = "SKU-9",
            OnHand = 3m,
            ReorderPoint = 10m,
            OccurredAt = DateTimeOffset.UnixEpoch,
        };

        await handler.HandleAsync(evt, Context(evt), CancellationToken.None);

        var alert = Assert.Single(h.Board.Snapshot("acme").RecentAlerts);
        Assert.Equal(AlertLevels.Warning, alert.Level);
        Assert.Contains("SKU-9", alert.Subject, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_energy_spike_becomes_a_warning()
    {
        var h = Build();
        var handler = new EnergySpikeDetectedHandler(h.Board, h.Processed);
        var evt = new EnergySpikeDetected
        {
            Tenant = "acme",
            MeterId = "main-incomer",
            Metric = "ActivePower",
            Value = 200m,
            Baseline = 100m,
            DeltaPercent = 100m,
            Unit = "kW",
            ReadingAt = DateTimeOffset.UnixEpoch,
        };

        await handler.HandleAsync(evt, Context(evt), CancellationToken.None);

        var alert = Assert.Single(h.Board.Snapshot("acme").RecentAlerts);
        Assert.Equal(nameof(EnergySpikeDetected), alert.Kind);
        Assert.Equal(AlertLevels.Warning, alert.Level);
        Assert.Contains("main-incomer", alert.Subject, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_delivery_degradation_becomes_a_warning()
    {
        var h = Build();
        var handler = new DeliveryHealthDegradedHandler(h.Board, h.Processed);
        var evt = new DeliveryHealthDegraded
        {
            Tenant = "acme",
            Transport = "webhook",
            ConsecutiveFailures = 3,
            Attempts = 5,
            Failed = 3,
            LastDetail = "503 Service Unavailable",
            DetectedAt = DateTimeOffset.UnixEpoch,
        };

        await handler.HandleAsync(evt, Context(evt), CancellationToken.None);

        var alert = Assert.Single(h.Board.Snapshot("acme").RecentAlerts);
        Assert.Equal(nameof(DeliveryHealthDegraded), alert.Kind);
        Assert.Equal(AlertLevels.Warning, alert.Level);
        Assert.Contains("webhook", alert.Subject, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Redelivering_the_same_alert_does_not_double_count_the_feed()
    {
        var h = Build();
        var handler = new QualityAlertRaisedHandler(h.Board, h.Processed);
        var evt = new QualityAlertRaised
        {
            Tenant = "acme",
            LineId = "line-1",
            ProductId = "widget",
            DefectRate = 0.08m,
            Threshold = 0.05m,
            InspectedAt = DateTimeOffset.UnixEpoch,
        };

        await handler.HandleAsync(evt, Context(evt), CancellationToken.None);
        await handler.HandleAsync(evt, Context(evt), CancellationToken.None); // same event id

        Assert.Single(h.Board.Snapshot("acme").RecentAlerts);
    }

    [Fact]
    public async Task A_closed_work_order_is_folded_into_an_informational_alert()
    {
        var h = Build();
        var handler = new WorkOrderClosedHandler(h.Board, h.Processed);
        var evt = new WorkOrderClosed
        {
            ClosedBy = "tech-1",
            WorkOrder = new()
            {
                Tenant = "acme",
                Number = "WO-1",
                Title = "Inspect PUMP-1",
                Status = "Closed",
                AssetCode = "PUMP-1",
            },
        };

        await handler.HandleAsync(evt, Context(evt), CancellationToken.None);
        await handler.HandleAsync(evt, Context(evt), CancellationToken.None); // same event id — idempotent

        var alert = Assert.Single(h.Board.Snapshot("acme").RecentAlerts);
        Assert.Equal(nameof(WorkOrderClosed), alert.Kind);
        Assert.Equal(AlertLevels.Info, alert.Level);
        Assert.Contains("WO-1", alert.Subject, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_quarantined_line_is_folded_into_a_warning_alert()
    {
        var h = Build();
        var handler = new QualityLineQuarantinedHandler(h.Board, h.Processed);
        var evt = new QualityLineQuarantined
        {
            Tenant = "acme",
            LineId = "line-1",
            QuarantinedBy = "inspector-1",
        };

        await handler.HandleAsync(evt, Context(evt), CancellationToken.None);
        await handler.HandleAsync(evt, Context(evt), CancellationToken.None); // same event id — idempotent

        var alert = Assert.Single(h.Board.Snapshot("acme").RecentAlerts);
        Assert.Equal(nameof(QualityLineQuarantined), alert.Kind);
        Assert.Equal(AlertLevels.Warning, alert.Level);
        Assert.Contains("line-1", alert.Subject, StringComparison.Ordinal);
    }
}
