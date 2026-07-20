using System.Collections.Concurrent;
using FactoryOS.Contracts.Events;
using FactoryOS.Contracts.StandardModel;
using FactoryOS.Plugins.Maintenance;
using FactoryOS.Plugins.RuleEngine;
using FactoryOS.Plugins.RuleEngine.Domain;
using Microsoft.Extensions.DependencyInjection;

namespace FactoryOS.IntegrationTests.Modules;

/// <summary>
/// The full observation-to-action loop over the real bus: a Standard Model reading crosses a configured rule
/// threshold, the Rule Engine emits <see cref="RuleTriggered"/>, and the Maintenance module — reacting only to the
/// shared event, never to the Rule Engine by name — raises a corrective work order and announces
/// <see cref="WorkOrderCreated"/>. Neither module references the other; the whole chain is data plus events.
/// </summary>
public sealed class RuleToMaintenanceChainTests
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

    private static MeterReadingReceived Reading(decimal value, Guid id) => new()
    {
        EventId = id,
        Reading = new MeterReading
        {
            Tenant = "acme",
            MeterId = "press-1",
            Metric = "Temperature",
            Value = value,
            Unit = "°C",
            ReadingAt = new DateTimeOffset(2026, 7, 20, 6, 0, 0, TimeSpan.Zero),
        },
    };

    [Fact]
    public async Task A_threshold_crossing_reading_raises_a_maintenance_work_order()
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
        new RuleEnginePlugin().ConfigureServices(services);
        new MaintenancePlugin().ConfigureServices(services);

        var sink = new CaptureSink();
        services.AddSingleton(sink);
        services.AddScoped<IEventHandler<WorkOrderCreated>, CapturingHandler<WorkOrderCreated>>();

        var provider = services.BuildServiceProvider();
        var bus = provider.GetRequiredService<IEventBus>();

        await bus.PublishAsync(Reading(90m, Guid.NewGuid())); // crosses threshold
        await bus.PublishAsync(Reading(80m, Guid.NewGuid())); // below threshold

        var created = Assert.Single(sink.Events.OfType<WorkOrderCreated>());
        Assert.Equal("Rule:overtemp-press-1", created.Reason);
        Assert.Equal("press-1", created.WorkOrder.AssetCode);
        Assert.Equal("Open", created.WorkOrder.Status);
    }
}
