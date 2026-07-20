using System.Collections.Concurrent;
using FactoryOS.Contracts.Events;
using FactoryOS.Plugins.Notification;
using FactoryOS.Plugins.Notification.Domain;
using FactoryOS.Plugins.Safety;
using FactoryOS.Plugins.Workflow;
using Microsoft.Extensions.DependencyInjection;

namespace FactoryOS.IntegrationTests.Modules;

/// <summary>
/// The full automation loop, proven over the real bus across three modules that never reference one another: a
/// reported safety incident becomes a Safety stand-down, the configuration-driven Workflow module turns it into
/// a requested action, and the Notification module routes that action to a transport and records it —
/// `SafetyIncidentReported → SafetyStandDownTriggered → WorkflowActionRequested → NotificationDispatched`.
/// </summary>
public sealed class SafetyToNotificationChainTests
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
    public async Task A_severe_incident_is_routed_all_the_way_to_a_dispatched_notification()
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
        services.AddSingleton(new NotificationOptions
        {
            ChannelTransports = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["ops"] = "sms" },
            DefaultTransport = "log",
        });
        new NotificationPlugin().ConfigureServices(services);

        var sink = new CaptureSink();
        services.AddSingleton(sink);
        services.AddScoped<IEventHandler<NotificationDispatched>, CapturingHandler<NotificationDispatched>>();

        var provider = services.BuildServiceProvider();
        var bus = provider.GetRequiredService<IEventBus>();
        var outbox = provider.GetRequiredService<INotificationOutbox>();

        await bus.PublishAsync(new SafetyIncidentReported
        {
            Tenant = "acme",
            SiteId = "site-1",
            Severity = 5,
            Category = "Chemical",
            OccurredAt = DateTimeOffset.UnixEpoch,
        });

        var dispatched = Assert.Single(sink.Events.OfType<NotificationDispatched>());
        Assert.Equal("ops", dispatched.Channel);
        Assert.Equal("sms", dispatched.Transport);
        Assert.Equal("Critical", dispatched.Priority);
        Assert.Equal("Notify", dispatched.Action);
        Assert.Contains("site-1", dispatched.Subject, StringComparison.Ordinal);

        var recorded = Assert.Single(outbox.ForTenant("acme"));
        Assert.Equal("sms", recorded.Transport);
    }
}
