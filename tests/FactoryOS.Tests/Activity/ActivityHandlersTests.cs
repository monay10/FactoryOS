using FactoryOS.Contracts.Events;
using FactoryOS.Contracts.StandardModel;
using FactoryOS.Plugins.Activity;
using FactoryOS.Plugins.Activity.Application;
using FactoryOS.Plugins.Activity.Domain;

namespace FactoryOS.Tests.Activity;

public sealed class ActivityHandlersTests
{
    private static EventContext Context(IIntegrationEvent e) =>
        new(Guid.NewGuid(), e.EventId, Guid.NewGuid(), null, "trace", EventPriority.Normal, 1, e.OccurredOnUtc);

    [Fact]
    public async Task A_fired_rule_becomes_a_rule_entry()
    {
        var feed = new InMemoryActivityFeed(new ActivityOptions());
        var handler = new RuleTriggeredHandler(feed);
        var evt = new RuleTriggered
        {
            Tenant = "acme",
            RuleId = "overtemp-press-1",
            Metric = "Temperature",
            MeterId = "press-1",
            Value = 90m,
            Operator = "GreaterThan",
            Threshold = 85m,
            Action = "RaiseMaintenanceAlert",
            TriggeredAt = DateTimeOffset.UnixEpoch,
            SourceEventId = Guid.NewGuid(),
        };

        await handler.HandleAsync(evt, Context(evt), CancellationToken.None);

        var entry = Assert.Single(feed.Recent("acme", 10));
        Assert.Equal("Rule", entry.Category);
        Assert.Contains("overtemp-press-1", entry.Headline, StringComparison.Ordinal);
        Assert.Equal(evt.EventId, entry.SourceEventId);
    }

    [Fact]
    public async Task A_work_order_becomes_a_maintenance_entry()
    {
        var feed = new InMemoryActivityFeed(new ActivityOptions());
        var handler = new WorkOrderCreatedHandler(feed);
        var evt = new WorkOrderCreated
        {
            Reason = "Rule:overtemp-press-1",
            WorkOrder = new WorkOrder
            {
                Tenant = "acme",
                Number = "WOR-ABC123",
                Title = "Rule fired",
                Status = "Open",
                AssetCode = "press-1",
            },
        };

        await handler.HandleAsync(evt, Context(evt), CancellationToken.None);

        var entry = Assert.Single(feed.Recent("acme", 10));
        Assert.Equal("Maintenance", entry.Category);
        Assert.Contains("WOR-ABC123", entry.Headline, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_closed_work_order_becomes_a_maintenance_entry_naming_who_closed_it()
    {
        var feed = new InMemoryActivityFeed(new ActivityOptions());
        var handler = new WorkOrderClosedHandler(feed);
        var evt = new WorkOrderClosed
        {
            ClosedBy = "tech-1",
            WorkOrder = new WorkOrder
            {
                Tenant = "acme",
                Number = "WOR-ABC123",
                Title = "Inspect press-1",
                Status = "Closed",
                AssetCode = "press-1",
            },
        };

        await handler.HandleAsync(evt, Context(evt), CancellationToken.None);

        var entry = Assert.Single(feed.Recent("acme", 10));
        Assert.Equal("Maintenance", entry.Category);
        Assert.Contains("WOR-ABC123", entry.Headline, StringComparison.Ordinal);
        Assert.Contains("closed by tech-1", entry.Headline, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_quarantined_line_becomes_a_quality_entry_naming_who_held_it()
    {
        var feed = new InMemoryActivityFeed(new ActivityOptions());
        var handler = new QualityLineQuarantinedHandler(feed);
        var evt = new QualityLineQuarantined
        {
            Tenant = "acme",
            LineId = "line-1",
            QuarantinedBy = "inspector-1",
            Reason = "burr on flange",
        };

        await handler.HandleAsync(evt, Context(evt), CancellationToken.None);

        var entry = Assert.Single(feed.Recent("acme", 10));
        Assert.Equal("Quality", entry.Category);
        Assert.Contains("line-1", entry.Headline, StringComparison.Ordinal);
        Assert.Contains("inspector-1", entry.Headline, StringComparison.Ordinal);
        Assert.Contains("burr on flange", entry.Headline, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_closed_work_order_with_no_actor_reads_as_closed_by_the_system()
    {
        var feed = new InMemoryActivityFeed(new ActivityOptions());
        var handler = new WorkOrderClosedHandler(feed);
        var evt = new WorkOrderClosed
        {
            WorkOrder = new WorkOrder
            {
                Tenant = "acme",
                Number = "WOR-XYZ",
                Title = "Inspect pump-2",
                Status = "Closed",
                AssetCode = "pump-2",
            },
        };

        await handler.HandleAsync(evt, Context(evt), CancellationToken.None);

        var entry = Assert.Single(feed.Recent("acme", 10));
        Assert.Contains("closed by the system", entry.Headline, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_safety_stand_down_becomes_a_safety_entry()
    {
        var feed = new InMemoryActivityFeed(new ActivityOptions());
        var handler = new SafetyStandDownTriggeredHandler(feed);
        var evt = new SafetyStandDownTriggered
        {
            Tenant = "acme",
            SiteId = "plant-1",
            Reason = "Frequency",
            TriggerSeverity = 3,
            WindowIncidentCount = 4,
            OccurredAt = DateTimeOffset.UnixEpoch,
            SourceEventId = Guid.NewGuid(),
        };

        await handler.HandleAsync(evt, Context(evt), CancellationToken.None);

        var entry = Assert.Single(feed.Recent("acme", 10));
        Assert.Equal("Safety", entry.Category);
        Assert.Contains("plant-1", entry.Headline, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_quality_alert_becomes_a_quality_entry()
    {
        var feed = new InMemoryActivityFeed(new ActivityOptions());
        var handler = new QualityAlertRaisedHandler(feed);
        var evt = new QualityAlertRaised
        {
            Tenant = "acme",
            LineId = "line-1",
            ProductId = "widget",
            DefectRate = 0.08m,
            Threshold = 0.05m,
            WindowInspectedUnits = 100,
            WindowDefectiveUnits = 8,
            InspectedAt = DateTimeOffset.UnixEpoch,
            SourceEventId = Guid.NewGuid(),
        };

        await handler.HandleAsync(evt, Context(evt), CancellationToken.None);

        var entry = Assert.Single(feed.Recent("acme", 10));
        Assert.Equal("Quality", entry.Category);
        Assert.Contains("line-1", entry.Headline, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_completed_production_order_becomes_a_production_entry()
    {
        var feed = new InMemoryActivityFeed(new ActivityOptions());
        var handler = new ProductionOrderCompletedHandler(feed);
        var evt = new ProductionOrderCompleted
        {
            Tenant = "acme",
            OrderId = "PO-7788",
            ProductId = "widget",
            TargetQuantity = 500,
            TotalProduced = 512,
            CompletedAt = DateTimeOffset.UnixEpoch,
            SourceEventId = Guid.NewGuid(),
        };

        await handler.HandleAsync(evt, Context(evt), CancellationToken.None);

        var entry = Assert.Single(feed.Recent("acme", 10));
        Assert.Equal("Production", entry.Category);
        Assert.Contains("PO-7788", entry.Headline, StringComparison.Ordinal);
        Assert.Contains("512/500", entry.Headline, StringComparison.Ordinal);
        Assert.Equal(evt.EventId, entry.SourceEventId);
    }

    [Fact]
    public async Task An_energy_spike_becomes_an_energy_entry()
    {
        var feed = new InMemoryActivityFeed(new ActivityOptions());
        var handler = new EnergySpikeDetectedHandler(feed);
        var evt = new EnergySpikeDetected
        {
            Tenant = "acme",
            MeterId = "press-1",
            Metric = "ActivePower",
            Value = 132.5m,
            Baseline = 100m,
            DeltaPercent = 32.5m,
            Unit = "kW",
            ReadingAt = DateTimeOffset.UnixEpoch,
        };

        await handler.HandleAsync(evt, Context(evt), CancellationToken.None);

        var entry = Assert.Single(feed.Recent("acme", 10));
        Assert.Equal("Energy", entry.Category);
        Assert.Contains("press-1", entry.Headline, StringComparison.Ordinal);
        Assert.Contains("32.5%", entry.Headline, StringComparison.Ordinal);
        Assert.Equal(evt.EventId, entry.SourceEventId);
    }

    [Fact]
    public async Task A_low_stock_crossing_becomes_a_warehouse_entry()
    {
        var feed = new InMemoryActivityFeed(new ActivityOptions());
        var handler = new LowStockDetectedHandler(feed);
        var evt = new LowStockDetected
        {
            Tenant = "acme",
            WarehouseId = "wh-main",
            Sku = "BOLT-M8",
            OnHand = 12m,
            ReorderPoint = 20m,
            OccurredAt = DateTimeOffset.UnixEpoch,
            SourceEventId = Guid.NewGuid(),
        };

        await handler.HandleAsync(evt, Context(evt), CancellationToken.None);

        var entry = Assert.Single(feed.Recent("acme", 10));
        Assert.Equal("Warehouse", entry.Category);
        Assert.Contains("BOLT-M8", entry.Headline, StringComparison.Ordinal);
        Assert.Contains("wh-main", entry.Headline, StringComparison.Ordinal);
        Assert.Equal(evt.EventId, entry.SourceEventId);
    }

    [Fact]
    public async Task A_certification_gap_becomes_a_compliance_entry()
    {
        var feed = new InMemoryActivityFeed(new ActivityOptions());
        var handler = new CertificationGapDetectedHandler(feed);
        var evt = new CertificationGapDetected
        {
            Tenant = "acme",
            ShiftId = "shift-night-1",
            WorkerId = "W-3391",
            RequiredCertification = "ForkliftLicense",
            Reason = "Expired",
            ExpiresAt = DateTimeOffset.UnixEpoch.AddDays(-1),
            ShiftStart = DateTimeOffset.UnixEpoch,
            SourceEventId = Guid.NewGuid(),
        };

        await handler.HandleAsync(evt, Context(evt), CancellationToken.None);

        var entry = Assert.Single(feed.Recent("acme", 10));
        Assert.Equal("Compliance", entry.Category);
        Assert.Contains("W-3391", entry.Headline, StringComparison.Ordinal);
        Assert.Contains("ForkliftLicense", entry.Headline, StringComparison.Ordinal);
        Assert.Contains("EXPIRED", entry.Headline, StringComparison.Ordinal);
        Assert.Equal(evt.EventId, entry.SourceEventId);
    }

    [Fact]
    public async Task An_ai_insight_becomes_an_insight_entry()
    {
        var feed = new InMemoryActivityFeed(new ActivityOptions());
        var handler = new InsightGeneratedHandler(feed);
        var evt = new InsightGenerated
        {
            Tenant = "acme",
            TriggerType = "SafetyStandDownTriggered",
            Subject = "plant-1",
            Insight = "Repeated near-misses on line 3 suggest a guarding gap; inspect before restart.",
            Model = "reasoning",
            GeneratedAt = DateTimeOffset.UnixEpoch,
            SourceEventId = Guid.NewGuid(),
        };

        await handler.HandleAsync(evt, Context(evt), CancellationToken.None);

        var entry = Assert.Single(feed.Recent("acme", 10));
        Assert.Equal("Insight", entry.Category);
        Assert.Contains("plant-1", entry.Headline, StringComparison.Ordinal);
        Assert.Contains("guarding gap", entry.Headline, StringComparison.Ordinal);
        Assert.Equal(evt.EventId, entry.SourceEventId);
    }
}
