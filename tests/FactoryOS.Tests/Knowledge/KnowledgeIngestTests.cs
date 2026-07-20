using FactoryOS.Agents.Knowledge;
using FactoryOS.Agents.Knowledge.Application;
using FactoryOS.Ai.Knowledge;
using FactoryOS.Contracts.Ai;
using FactoryOS.Contracts.Events;
using FactoryOS.Contracts.StandardModel;
using FactoryOS.Domain.Results;

namespace FactoryOS.Tests.Knowledge;

public sealed class KnowledgeIngestTests
{
    private sealed class RecordingIndexer : IKnowledgeIndexer
    {
        public List<(KnowledgeDocument Document, string Model)> Calls { get; } = [];

        public Result<int> Next { get; set; } = Result.Success(1);

        public Task<Result<int>> IngestAsync(KnowledgeDocument document, string embeddingModel, CancellationToken cancellationToken)
        {
            Calls.Add((document, embeddingModel));
            return Task.FromResult(Next);
        }
    }

    private static EventContext Context(IIntegrationEvent e) =>
        new(Guid.NewGuid(), e.EventId, Guid.NewGuid(), null, "trace", EventPriority.Normal, 1, e.OccurredOnUtc);

    private static KnowledgeIngestor Ingestor(RecordingIndexer indexer, string model = "embed") =>
        new(indexer, new KnowledgeIngestOptions { EmbeddingModel = model });

    [Fact]
    public async Task Ingesting_builds_a_tenant_scoped_document_with_the_configured_model()
    {
        var indexer = new RecordingIndexer();
        var ingestor = Ingestor(indexer, "text-embed-3");

        await ingestor.IngestAsync(
            new KnowledgeSignal("acme", "activity/rule/abc", "Rule fired on press-1.", Guid.NewGuid()),
            CancellationToken.None);

        var (document, model) = Assert.Single(indexer.Calls);
        Assert.Equal("acme", document.Tenant);
        Assert.Equal("activity/rule/abc", document.Source);
        Assert.Equal("Rule fired on press-1.", document.Text);
        Assert.Equal("text-embed-3", model);
    }

    [Fact]
    public async Task An_indexer_failure_throws_so_the_bus_retries()
    {
        var indexer = new RecordingIndexer { Next = Result.Failure<int>(Error.Failure("Ai.Down", "embedding provider unreachable")) };
        var ingestor = Ingestor(indexer);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            ingestor.IngestAsync(new KnowledgeSignal("acme", "activity/rule/abc", "text", Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task A_fired_rule_is_narrated_with_a_rule_source()
    {
        var indexer = new RecordingIndexer();
        var handler = new RuleTriggeredHandler(Ingestor(indexer));
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

        var (document, _) = Assert.Single(indexer.Calls);
        Assert.StartsWith("activity/rule/", document.Source, StringComparison.Ordinal);
        Assert.Contains("overtemp-press-1", document.Text, StringComparison.Ordinal);
        Assert.Contains("press-1", document.Text, StringComparison.Ordinal);
        Assert.Contains("RaiseMaintenanceAlert", document.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_work_order_is_narrated_with_a_workorder_source()
    {
        var indexer = new RecordingIndexer();
        var handler = new WorkOrderCreatedHandler(Ingestor(indexer));
        var evt = new WorkOrderCreated
        {
            Reason = "Rule:overtemp-press-1",
            WorkOrder = new WorkOrder
            {
                Tenant = "acme",
                Number = "WOR-ABC123",
                Title = "Investigate overtemp",
                Status = "Open",
                AssetCode = "press-1",
            },
        };

        await handler.HandleAsync(evt, Context(evt), CancellationToken.None);

        var (document, _) = Assert.Single(indexer.Calls);
        Assert.StartsWith("activity/workorder/", document.Source, StringComparison.Ordinal);
        Assert.Contains("WOR-ABC123", document.Text, StringComparison.Ordinal);
        Assert.Equal("acme", document.Tenant);
    }

    [Fact]
    public async Task A_safety_stand_down_is_narrated_with_a_safety_source()
    {
        var indexer = new RecordingIndexer();
        var handler = new SafetyStandDownTriggeredHandler(Ingestor(indexer));
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

        var (document, _) = Assert.Single(indexer.Calls);
        Assert.StartsWith("activity/safety/", document.Source, StringComparison.Ordinal);
        Assert.Contains("plant-1", document.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_quality_alert_is_narrated_with_a_quality_source()
    {
        var indexer = new RecordingIndexer();
        var handler = new QualityAlertRaisedHandler(Ingestor(indexer));
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

        var (document, _) = Assert.Single(indexer.Calls);
        Assert.StartsWith("activity/quality/", document.Source, StringComparison.Ordinal);
        Assert.Contains("line-1", document.Text, StringComparison.Ordinal);
        Assert.Contains("widget", document.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_completed_production_order_is_narrated_with_a_production_source()
    {
        var indexer = new RecordingIndexer();
        var handler = new ProductionOrderCompletedHandler(Ingestor(indexer));
        var evt = new ProductionOrderCompleted
        {
            Tenant = "acme",
            OrderId = "PO-42",
            ProductId = "widget",
            TargetQuantity = 500,
            TotalProduced = 512,
            CompletedAt = DateTimeOffset.UnixEpoch,
            SourceEventId = Guid.NewGuid(),
        };

        await handler.HandleAsync(evt, Context(evt), CancellationToken.None);

        var (document, _) = Assert.Single(indexer.Calls);
        Assert.StartsWith("activity/production/", document.Source, StringComparison.Ordinal);
        Assert.Contains("PO-42", document.Text, StringComparison.Ordinal);
        Assert.Contains("widget", document.Text, StringComparison.Ordinal);
        Assert.Contains("512", document.Text, StringComparison.Ordinal);
        Assert.Equal("acme", document.Tenant);
    }

    [Fact]
    public async Task A_degraded_delivery_is_narrated_with_a_delivery_source()
    {
        var indexer = new RecordingIndexer();
        var handler = new DeliveryHealthDegradedHandler(Ingestor(indexer));
        var evt = new DeliveryHealthDegraded
        {
            Tenant = "acme",
            Transport = "webhook",
            ConsecutiveFailures = 3,
            Attempts = 10,
            Failed = 4,
            LastDetail = "HTTP 503 from provider",
            DetectedAt = DateTimeOffset.UnixEpoch,
            SourceEventId = Guid.NewGuid(),
        };

        await handler.HandleAsync(evt, Context(evt), CancellationToken.None);

        var (document, _) = Assert.Single(indexer.Calls);
        Assert.StartsWith("activity/delivery/", document.Source, StringComparison.Ordinal);
        Assert.Contains("webhook", document.Text, StringComparison.Ordinal);
        Assert.Contains("HTTP 503 from provider", document.Text, StringComparison.Ordinal);
        Assert.Equal("acme", document.Tenant);
    }

    [Fact]
    public async Task An_energy_spike_is_narrated_with_an_energy_source()
    {
        var indexer = new RecordingIndexer();
        var handler = new EnergySpikeDetectedHandler(Ingestor(indexer));
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

        var (document, _) = Assert.Single(indexer.Calls);
        Assert.StartsWith("activity/energy/", document.Source, StringComparison.Ordinal);
        Assert.Contains("main-incomer", document.Text, StringComparison.Ordinal);
        Assert.Contains("ActivePower", document.Text, StringComparison.Ordinal);
        Assert.Equal("acme", document.Tenant);
    }

    [Fact]
    public async Task A_low_stock_crossing_is_narrated_with_a_warehouse_source()
    {
        var indexer = new RecordingIndexer();
        var handler = new LowStockDetectedHandler(Ingestor(indexer));
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

        var (document, _) = Assert.Single(indexer.Calls);
        Assert.StartsWith("activity/warehouse/", document.Source, StringComparison.Ordinal);
        Assert.Contains("BOLT-M8", document.Text, StringComparison.Ordinal);
        Assert.Contains("wh-main", document.Text, StringComparison.Ordinal);
        Assert.Equal("acme", document.Tenant);
    }

    [Fact]
    public async Task A_certification_gap_is_narrated_with_a_compliance_source()
    {
        var indexer = new RecordingIndexer();
        var handler = new CertificationGapDetectedHandler(Ingestor(indexer));
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

        var (document, _) = Assert.Single(indexer.Calls);
        Assert.StartsWith("activity/compliance/", document.Source, StringComparison.Ordinal);
        Assert.Contains("W-3391", document.Text, StringComparison.Ordinal);
        Assert.Contains("ForkliftLicense", document.Text, StringComparison.Ordinal);
        Assert.Equal("acme", document.Tenant);
    }

    [Fact]
    public async Task An_ai_insight_is_narrated_with_an_insight_source()
    {
        var indexer = new RecordingIndexer();
        var handler = new InsightGeneratedHandler(Ingestor(indexer));
        var evt = new InsightGenerated
        {
            Tenant = "acme",
            TriggerType = "QualityAlertRaised",
            Subject = "line-1 / widget",
            Insight = "Defect rate rose after the die change; verify die alignment.",
            Model = "reasoning",
            GeneratedAt = DateTimeOffset.UnixEpoch,
            SourceEventId = Guid.NewGuid(),
        };

        await handler.HandleAsync(evt, Context(evt), CancellationToken.None);

        var (document, _) = Assert.Single(indexer.Calls);
        Assert.StartsWith("activity/insight/", document.Source, StringComparison.Ordinal);
        Assert.Contains("line-1", document.Text, StringComparison.Ordinal);
        Assert.Contains("die alignment", document.Text, StringComparison.Ordinal);
        Assert.Equal("acme", document.Tenant);
    }
}
