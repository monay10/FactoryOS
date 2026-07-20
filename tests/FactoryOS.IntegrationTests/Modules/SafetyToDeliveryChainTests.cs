using System.Collections.Concurrent;
using FactoryOS.Connectors.Log;
using FactoryOS.Connectors.Log.Domain;
using FactoryOS.Contracts.Events;
using FactoryOS.Plugins.Notification;
using FactoryOS.Plugins.Safety;
using FactoryOS.Plugins.Workflow;
using Microsoft.Extensions.DependencyInjection;

namespace FactoryOS.IntegrationTests.Modules;

/// <summary>
/// The complete alert-to-outside-world loop, proven over the real bus across four plugins that never reference
/// one another — including the first outbound connector, the door out. A severe safety incident travels the
/// whole spine and leaves the system through the log transport:
/// `SafetyIncidentReported → SafetyStandDownTriggered → WorkflowActionRequested → NotificationDispatched →
/// NotificationDelivered`, landing in the delivery journal.
/// </summary>
public sealed class SafetyToDeliveryChainTests
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
    public async Task A_severe_incident_leaves_the_system_through_the_log_transport()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEventBus();
        new SafetyPlugin().ConfigureServices(services);
        services.AddSingleton(new WorkflowOptions
        {
            Rules = [new WorkflowRule { Trigger = "SafetyStandDownTriggered", Action = "Notify", Priority = "Critical", Channel = "ops" }],
        });
        new WorkflowPlugin().ConfigureServices(services);
        services.AddSingleton(new NotificationOptions { DefaultTransport = "log" }); // ops → log
        new NotificationPlugin().ConfigureServices(services);
        new LogConnectorPlugin().ConfigureServices(services); // transport "log"

        var sink = new CaptureSink();
        services.AddSingleton(sink);
        services.AddScoped<IEventHandler<NotificationDelivered>, CapturingHandler<NotificationDelivered>>();

        var provider = services.BuildServiceProvider();
        var bus = provider.GetRequiredService<IEventBus>();
        var journal = provider.GetRequiredService<IDeliveryJournal>();

        await bus.PublishAsync(new SafetyIncidentReported
        {
            Tenant = "acme",
            SiteId = "site-1",
            Severity = 5,
            Category = "Chemical",
            OccurredAt = DateTimeOffset.UnixEpoch,
        });

        var delivered = Assert.Single(sink.Events.OfType<NotificationDelivered>());
        Assert.True(delivered.Delivered);
        Assert.Equal("log", delivered.Transport);
        Assert.Contains("site-1", delivered.Subject, StringComparison.Ordinal);

        var journaled = Assert.Single(journal.ForTenant("acme"));
        Assert.Equal("ops", journaled.Channel);
        Assert.Equal("Notify", journaled.Action);
    }
}
