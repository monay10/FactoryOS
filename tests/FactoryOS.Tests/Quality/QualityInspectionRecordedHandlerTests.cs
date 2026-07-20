using FactoryOS.Contracts.Events;
using FactoryOS.Plugins.Quality;
using FactoryOS.Plugins.Quality.Application;
using FactoryOS.Plugins.Quality.Domain;

namespace FactoryOS.Tests.Quality;

public sealed class QualityInspectionRecordedHandlerTests
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

    private sealed record Harness(QualityInspectionRecordedHandler Handler, RecordingEventBus Bus);

    private static Harness Build(decimal threshold = 0.05m, int minimum = 20, int window = 20)
    {
        var bus = new RecordingEventBus();
        var options = new QualityOptions { DefectRateThreshold = threshold, MinimumInspectedUnits = minimum, WindowSize = window };
        var handler = new QualityInspectionRecordedHandler(
            bus,
            new InMemoryDefectRateWindowStore(options.WindowSize),
            new InMemoryProcessedEventLog(),
            options);
        return new Harness(handler, bus);
    }

    private static QualityInspectionRecorded Inspection(int inspected, int defective) => new()
    {
        Tenant = "acme",
        LineId = "line-1",
        ProductId = "widget",
        InspectedUnits = inspected,
        DefectiveUnits = defective,
        InspectedAt = DateTimeOffset.UnixEpoch,
    };

    private static EventContext Context(IIntegrationEvent e) =>
        new(Guid.NewGuid(), e.EventId, Guid.NewGuid(), null, "trace", EventPriority.Normal, 1, e.OccurredOnUtc);

    [Fact]
    public async Task Raises_an_alert_when_the_rolling_rate_breaches_the_threshold()
    {
        var h = Build();
        var inspection = Inspection(inspected: 100, defective: 6); // 6% > 5%, 100 >= 20

        await h.Handler.HandleAsync(inspection, Context(inspection), CancellationToken.None);

        var alert = Assert.Single(h.Bus.Published.OfType<QualityAlertRaised>());
        Assert.Equal("line-1", alert.LineId);
        Assert.Equal("widget", alert.ProductId);
        Assert.Equal(0.06m, alert.DefectRate);
        Assert.Equal(0.05m, alert.Threshold);
        Assert.Equal(100, alert.WindowInspectedUnits);
        Assert.Equal(6, alert.WindowDefectiveUnits);
        Assert.Equal(inspection.EventId, alert.SourceEventId);
    }

    [Fact]
    public async Task Stays_silent_below_the_evidence_floor()
    {
        var h = Build();
        var inspection = Inspection(inspected: 10, defective: 5); // 50% but only 10 units

        await h.Handler.HandleAsync(inspection, Context(inspection), CancellationToken.None);

        Assert.Empty(h.Bus.Published);
    }

    [Fact]
    public async Task An_empty_inspection_is_a_no_op()
    {
        var h = Build();
        var inspection = Inspection(inspected: 0, defective: 0);

        await h.Handler.HandleAsync(inspection, Context(inspection), CancellationToken.None);

        Assert.Empty(h.Bus.Published);
    }

    [Fact]
    public async Task Redelivery_of_the_same_inspection_is_ignored()
    {
        var h = Build();
        var inspection = Inspection(inspected: 100, defective: 6);

        await h.Handler.HandleAsync(inspection, Context(inspection), CancellationToken.None);
        await h.Handler.HandleAsync(inspection, Context(inspection), CancellationToken.None); // same event id

        Assert.Single(h.Bus.Published.OfType<QualityAlertRaised>());
    }

    [Fact]
    public async Task A_rate_at_the_threshold_does_not_alert()
    {
        var h = Build();
        var inspection = Inspection(inspected: 100, defective: 5); // exactly 5%

        await h.Handler.HandleAsync(inspection, Context(inspection), CancellationToken.None);

        Assert.Empty(h.Bus.Published);
    }
}
