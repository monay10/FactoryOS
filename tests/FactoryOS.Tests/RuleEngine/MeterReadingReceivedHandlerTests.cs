using FactoryOS.Contracts.Events;
using FactoryOS.Contracts.StandardModel;
using FactoryOS.Plugins.RuleEngine;
using FactoryOS.Plugins.RuleEngine.Application;
using FactoryOS.Plugins.RuleEngine.Domain;

namespace FactoryOS.Tests.RuleEngine;

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

    private sealed record Harness(MeterReadingReceivedHandler Handler, RecordingEventBus Bus);

    private static Harness Build(params RuleDefinition[] rules)
    {
        var bus = new RecordingEventBus();
        var options = new RuleEngineOptions { Rules = rules };
        return new Harness(new MeterReadingReceivedHandler(bus, options, new InMemoryRuleFiringLog()), bus);
    }

    private static MeterReadingReceived Reading(string metric, decimal value, Guid? id = null) => new()
    {
        EventId = id ?? Guid.NewGuid(),
        Reading = new MeterReading
        {
            Tenant = "acme",
            MeterId = "press-1",
            Metric = metric,
            Value = value,
            Unit = "°C",
            ReadingAt = DateTimeOffset.UnixEpoch,
        },
    };

    private static EventContext Context(IIntegrationEvent e) =>
        new(Guid.NewGuid(), e.EventId, Guid.NewGuid(), null, "trace", EventPriority.Normal, 1, e.OccurredOnUtc);

    private static readonly RuleDefinition OverTemp = new()
    {
        Id = "overtemp-press-1",
        Metric = "Temperature",
        Operator = ComparisonOperator.GreaterThan,
        Threshold = 85m,
        Action = "RaiseMaintenanceAlert",
    };

    [Fact]
    public async Task A_matching_reading_triggers_the_rule()
    {
        var h = Build(OverTemp);
        var evt = Reading("Temperature", 90m);

        await h.Handler.HandleAsync(evt, Context(evt), CancellationToken.None);

        var triggered = Assert.Single(h.Bus.Published.OfType<RuleTriggered>());
        Assert.Equal("overtemp-press-1", triggered.RuleId);
        Assert.Equal("acme", triggered.Tenant);
        Assert.Equal("press-1", triggered.MeterId);
        Assert.Equal(90m, triggered.Value);
        Assert.Equal("GreaterThan", triggered.Operator);
        Assert.Equal(85m, triggered.Threshold);
        Assert.Equal("RaiseMaintenanceAlert", triggered.Action);
        Assert.Equal(DateTimeOffset.UnixEpoch, triggered.TriggeredAt);
        Assert.Equal(evt.EventId, triggered.SourceEventId);
    }

    [Fact]
    public async Task A_reading_below_the_threshold_triggers_nothing()
    {
        var h = Build(OverTemp);
        var evt = Reading("Temperature", 80m);

        await h.Handler.HandleAsync(evt, Context(evt), CancellationToken.None);

        Assert.Empty(h.Bus.Published);
    }

    [Fact]
    public async Task A_reading_for_another_metric_is_ignored()
    {
        var h = Build(OverTemp);
        var evt = Reading("Pressure", 999m);

        await h.Handler.HandleAsync(evt, Context(evt), CancellationToken.None);

        Assert.Empty(h.Bus.Published);
    }

    [Fact]
    public async Task Metric_matching_is_case_insensitive()
    {
        var h = Build(OverTemp);
        var evt = Reading("temperature", 90m);

        await h.Handler.HandleAsync(evt, Context(evt), CancellationToken.None);

        Assert.Single(h.Bus.Published.OfType<RuleTriggered>());
    }

    [Fact]
    public async Task One_reading_can_trigger_several_rules()
    {
        var h = Build(
            OverTemp,
            new RuleDefinition
            {
                Id = "very-hot",
                Metric = "Temperature",
                Operator = ComparisonOperator.GreaterOrEqual,
                Threshold = 90m,
                Action = "EmergencyStop",
            });
        var evt = Reading("Temperature", 90m);

        await h.Handler.HandleAsync(evt, Context(evt), CancellationToken.None);

        var triggered = h.Bus.Published.OfType<RuleTriggered>().ToList();
        Assert.Equal(2, triggered.Count);
        Assert.Contains(triggered, t => t.RuleId == "overtemp-press-1");
        Assert.Contains(triggered, t => t.RuleId == "very-hot");
    }

    [Fact]
    public async Task Redelivery_of_the_same_reading_fires_each_rule_once()
    {
        var h = Build(OverTemp);
        var evt = Reading("Temperature", 90m);

        await h.Handler.HandleAsync(evt, Context(evt), CancellationToken.None);
        await h.Handler.HandleAsync(evt, Context(evt), CancellationToken.None); // same event id

        Assert.Single(h.Bus.Published.OfType<RuleTriggered>());
    }
}
