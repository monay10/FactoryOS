using FactoryOS.Contracts.Events;
using FactoryOS.Plugins.Safety;
using FactoryOS.Plugins.Safety.Application;
using FactoryOS.Plugins.Safety.Domain;

namespace FactoryOS.Tests.Safety;

public sealed class SafetyIncidentReportedHandlerTests
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

    private sealed record Harness(SafetyIncidentReportedHandler Handler, RecordingEventBus Bus);

    private static Harness Build(int standDownSeverity = 4, int frequencyThreshold = 3, int window = 10)
    {
        var bus = new RecordingEventBus();
        var options = new SafetyOptions { StandDownSeverity = standDownSeverity, FrequencyThreshold = frequencyThreshold, WindowSize = window };
        var handler = new SafetyIncidentReportedHandler(bus, new InMemoryIncidentWindowStore(options.WindowSize), new InMemoryProcessedEventLog(), options);
        return new Harness(handler, bus);
    }

    private static SafetyIncidentReported Incident(int severity, string site = "site-1") => new()
    {
        Tenant = "acme",
        SiteId = site,
        Severity = severity,
        OccurredAt = DateTimeOffset.UnixEpoch,
    };

    private static EventContext Context(IIntegrationEvent e) =>
        new(Guid.NewGuid(), e.EventId, Guid.NewGuid(), null, "trace", EventPriority.Normal, 1, e.OccurredOnUtc);

    private static async Task Report(Harness h, SafetyIncidentReported incident) =>
        await h.Handler.HandleAsync(incident, Context(incident), CancellationToken.None);

    [Fact]
    public async Task A_severe_incident_triggers_a_high_severity_stand_down()
    {
        var h = Build();

        await Report(h, Incident(severity: 5));

        var standDown = Assert.Single(h.Bus.Published.OfType<SafetyStandDownTriggered>());
        Assert.Equal("HighSeverity", standDown.Reason);
        Assert.Equal(5, standDown.TriggerSeverity);
        Assert.Equal("site-1", standDown.SiteId);
    }

    [Fact]
    public async Task Accumulating_minor_incidents_triggers_a_frequency_stand_down()
    {
        var h = Build(frequencyThreshold: 3);

        await Report(h, Incident(severity: 1));
        await Report(h, Incident(severity: 2));
        Assert.Empty(h.Bus.Published);

        await Report(h, Incident(severity: 1)); // third → frequency

        var standDown = Assert.Single(h.Bus.Published.OfType<SafetyStandDownTriggered>());
        Assert.Equal("Frequency", standDown.Reason);
        Assert.Equal(3, standDown.WindowIncidentCount);
    }

    [Fact]
    public async Task Incidents_at_different_sites_do_not_combine()
    {
        var h = Build(frequencyThreshold: 3);

        await Report(h, Incident(severity: 1, site: "site-1"));
        await Report(h, Incident(severity: 1, site: "site-2"));
        await Report(h, Incident(severity: 1, site: "site-1"));

        Assert.Empty(h.Bus.Published); // neither site reached 3
    }

    [Fact]
    public async Task Redelivery_of_the_same_incident_is_not_double_counted()
    {
        var h = Build(frequencyThreshold: 3);
        var incident = Incident(severity: 1);

        await Report(h, incident);
        await Report(h, incident); // same event id
        await Report(h, Incident(severity: 1));

        Assert.Empty(h.Bus.Published); // only 2 distinct incidents, threshold is 3
    }
}
