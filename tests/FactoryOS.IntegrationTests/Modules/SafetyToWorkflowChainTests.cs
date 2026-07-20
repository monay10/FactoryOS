using System.Collections.Concurrent;
using FactoryOS.Contracts.Events;
using FactoryOS.Plugins.Safety;
using FactoryOS.Plugins.Workflow;
using Microsoft.Extensions.DependencyInjection;

namespace FactoryOS.IntegrationTests.Modules;

/// <summary>
/// The capstone cross-module test: a reported safety incident becomes — purely over the real event bus — a
/// Safety stand-down, which the configuration-driven Workflow module turns into a requested action. Three
/// modules' worth of behaviour (Safety producing, Workflow reacting per its rules) chained with no module
/// referencing another. `SafetyIncidentReported → SafetyStandDownTriggered → WorkflowActionRequested`.
/// </summary>
public sealed class SafetyToWorkflowChainTests
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
    public async Task A_severe_incident_becomes_a_configured_workflow_action()
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

        var sink = new CaptureSink();
        services.AddSingleton(sink);
        services.AddScoped<IEventHandler<WorkflowActionRequested>, CapturingHandler<WorkflowActionRequested>>();

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

        var action = Assert.Single(sink.Events.OfType<WorkflowActionRequested>());
        Assert.Equal("SafetyStandDownTriggered", action.TriggerType);
        Assert.Equal("Notify", action.Action);
        Assert.Equal("Critical", action.Priority);
        Assert.Equal("ops", action.Channel);
        Assert.Contains("site-1", action.Subject, StringComparison.Ordinal);
    }
}
