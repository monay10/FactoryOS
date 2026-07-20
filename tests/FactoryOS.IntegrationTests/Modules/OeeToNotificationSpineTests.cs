using System.Collections.Concurrent;
using FactoryOS.Contracts.Events;
using FactoryOS.Plugins.RuleEngine;
using FactoryOS.Plugins.RuleEngine.Domain;
using FactoryOS.Plugins.Workflow;
using Microsoft.Extensions.DependencyInjection;

namespace FactoryOS.IntegrationTests.Modules;

/// <summary>
/// The OEE-degradation automation spine over one bus, two plugins, zero inter-module references: a computed
/// <see cref="OeeCalculated"/> crosses a rule threshold (Rule Engine → <see cref="RuleTriggered"/>), which the
/// Workflow module routes straight to a notification request on the ops channel (Workflow →
/// <see cref="WorkflowActionRequested"/>) — the path that gives a non-work-order rule action (for example
/// <c>NotifyEnergyDesk</c>) a destination without passing through Maintenance. Each stage speaks only the shared
/// vocabulary; the chain is data plus events.
/// </summary>
public sealed class OeeToNotificationSpineTests
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
    public async Task Degraded_oee_flows_through_a_rule_to_a_notification_request()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEventBus();

        services.AddSingleton(new RuleEngineOptions
        {
            Rules =
            [
                new RuleDefinition
                {
                    Id = "oee-degraded",
                    Metric = "Oee",
                    Operator = ComparisonOperator.LessThan,
                    Threshold = 0.6m,
                    Action = "RaiseMaintenanceAlert",
                },
            ],
        });
        services.AddSingleton(new WorkflowOptions
        {
            Rules = [new WorkflowRule { Trigger = "RuleTriggered", Action = "Notify", Priority = "High", Channel = "ops" }],
        });

        new RuleEnginePlugin().ConfigureServices(services);
        new WorkflowPlugin().ConfigureServices(services);

        var sink = new CaptureSink();
        services.AddSingleton(sink);
        services.AddScoped<IEventHandler<WorkflowActionRequested>, CapturingHandler<WorkflowActionRequested>>();

        var provider = services.BuildServiceProvider();
        var bus = provider.GetRequiredService<IEventBus>();

        await bus.PublishAsync(new OeeCalculated
        {
            Tenant = "acme",
            MachineId = "press-1",
            PeriodStart = new DateTimeOffset(2026, 7, 20, 5, 0, 0, TimeSpan.Zero),
            PeriodEnd = new DateTimeOffset(2026, 7, 20, 6, 0, 0, TimeSpan.Zero),
            Availability = 0.9m,
            Performance = 0.7m,
            Quality = 0.85m,
            Oee = 0.53m,
            MeetsTarget = false,
        });

        var action = Assert.Single(sink.Events.OfType<WorkflowActionRequested>());
        Assert.Equal("RuleTriggered", action.TriggerType);
        Assert.Equal("Notify", action.Action);
        Assert.Equal("ops", action.Channel);
        Assert.Contains("oee-degraded", action.Subject, StringComparison.Ordinal);
    }
}
