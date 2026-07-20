using System.Collections.Concurrent;
using FactoryOS.Contracts.Events;
using FactoryOS.Plugins.Safety;
using Microsoft.Extensions.DependencyInjection;

namespace FactoryOS.IntegrationTests.Modules;

/// <summary>
/// Proves the Safety module works event-driven through the real in-process event bus: a
/// <see cref="SafetyIncidentReported"/> published on the bus is consumed by the plugin's handler, which
/// publishes a <see cref="SafetyStandDownTriggered"/> back when the incident is severe enough — no module
/// referencing another, only the bus.
/// </summary>
public sealed class SafetyModuleTests
{
    private sealed class CaptureSink
    {
        public ConcurrentBag<IIntegrationEvent> Events { get; } = [];
    }

    private sealed class CapturingHandler<TEvent> : IEventHandler<TEvent>
        where TEvent : IIntegrationEvent
    {
        private readonly CaptureSink _sink;

        public CapturingHandler(CaptureSink sink) => _sink = sink;

        public Task HandleAsync(TEvent integrationEvent, EventContext context, CancellationToken cancellationToken)
        {
            _sink.Events.Add(integrationEvent);
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task A_severe_incident_on_the_bus_yields_a_stand_down()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEventBus();
        new SafetyPlugin().ConfigureServices(services);

        var sink = new CaptureSink();
        services.AddSingleton(sink);
        services.AddScoped<IEventHandler<SafetyStandDownTriggered>, CapturingHandler<SafetyStandDownTriggered>>();

        var provider = services.BuildServiceProvider();
        var bus = provider.GetRequiredService<IEventBus>();

        await bus.PublishAsync(new SafetyIncidentReported
        {
            Tenant = "acme",
            SiteId = "site-1",
            Severity = 5,
            Category = "Chemical",
            OccurredAt = DateTimeOffset.UnixEpoch,
        });

        var standDown = Assert.Single(sink.Events.OfType<SafetyStandDownTriggered>());
        Assert.Equal("site-1", standDown.SiteId);
        Assert.Equal("HighSeverity", standDown.Reason);
    }
}
