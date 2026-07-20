using System.Collections.Concurrent;
using FactoryOS.Contracts.Events;
using FactoryOS.Contracts.StandardModel;
using FactoryOS.Plugins.Maintenance;
using FactoryOS.Plugins.RuleEngine;
using FactoryOS.Plugins.RuleEngine.Domain;
using FactoryOS.Plugins.Workflow;
using Microsoft.Extensions.DependencyInjection;

namespace FactoryOS.IntegrationTests.Modules;

/// <summary>
/// The whole automation spine over one bus, four plugins, zero inter-module references: a Standard Model reading
/// crosses a rule threshold (Rule Engine → <see cref="RuleTriggered"/>), which raises a corrective work order
/// (Maintenance → <see cref="WorkOrderCreated"/>), which is escalated to a notification request (Workflow →
/// <see cref="WorkflowActionRequested"/>). Each stage speaks only the shared vocabulary; the chain is data plus
/// events, and removing any plugin removes exactly its hop.
/// </summary>
public sealed class ReadingToWorkflowSpineTests
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
    public async Task A_reading_crossing_a_threshold_flows_all_the_way_to_a_notification_request()
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
                    Id = "overtemp-press-1",
                    Metric = "Temperature",
                    Operator = ComparisonOperator.GreaterThan,
                    Threshold = 85m,
                    Action = "RaiseMaintenanceAlert",
                },
            ],
        });
        services.AddSingleton(new WorkflowOptions
        {
            Rules = [new WorkflowRule { Trigger = "WorkOrderCreated", Action = "Notify", Priority = "High", Channel = "maintenance" }],
        });

        new RuleEnginePlugin().ConfigureServices(services);
        new MaintenancePlugin().ConfigureServices(services);
        new WorkflowPlugin().ConfigureServices(services);

        var sink = new CaptureSink();
        services.AddSingleton(sink);
        services.AddScoped<IEventHandler<WorkflowActionRequested>, CapturingHandler<WorkflowActionRequested>>();

        var provider = services.BuildServiceProvider();
        var bus = provider.GetRequiredService<IEventBus>();

        await bus.PublishAsync(new MeterReadingReceived
        {
            Reading = new MeterReading
            {
                Tenant = "acme",
                MeterId = "press-1",
                Metric = "Temperature",
                Value = 90m,
                Unit = "°C",
                ReadingAt = new DateTimeOffset(2026, 7, 20, 6, 0, 0, TimeSpan.Zero),
            },
        });

        var action = Assert.Single(sink.Events.OfType<WorkflowActionRequested>());
        Assert.Equal("WorkOrderCreated", action.TriggerType);
        Assert.Equal("Notify", action.Action);
        Assert.Equal("maintenance", action.Channel);
        Assert.Contains("WOR-", action.Subject, StringComparison.Ordinal);
    }
}
