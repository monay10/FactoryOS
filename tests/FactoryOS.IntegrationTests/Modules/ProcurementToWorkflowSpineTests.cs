using System.Collections.Concurrent;
using FactoryOS.Contracts.Events;
using FactoryOS.Plugins.Procurement;
using FactoryOS.Plugins.Warehouse;
using FactoryOS.Plugins.Workflow;
using FactoryOS.Plugins.Workflow.Domain;
using Microsoft.Extensions.DependencyInjection;

namespace FactoryOS.IntegrationTests.Modules;

/// <summary>
/// The replenishment automation spine over one bus, three plugins, zero inter-module references: a stock movement
/// crosses an item's reorder point (Warehouse → <see cref="LowStockDetected"/>), Procurement raises a purchase
/// requisition (<see cref="PurchaseRequisitionRaised"/>), and the Workflow module routes that requisition straight
/// to a notification request on the procurement channel (Workflow → <see cref="WorkflowActionRequested"/>) — the
/// path that gives a raised requisition a destination. Each stage speaks only the shared vocabulary; the chain is
/// data plus events. `StockMovementRecorded → LowStockDetected → PurchaseRequisitionRaised → WorkflowActionRequested`.
/// </summary>
public sealed class ProcurementToWorkflowSpineTests
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
    public async Task A_raised_requisition_flows_through_a_rule_to_a_notification_request()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEventBus();

        services.AddSingleton(new WorkflowOptions
        {
            Rules = [new WorkflowRule { Trigger = "PurchaseRequisitionRaised", Action = "Notify", Priority = "Normal", Channel = "procurement" }],
        });

        new WarehousePlugin().ConfigureServices(services);
        new ProcurementPlugin().ConfigureServices(services);
        new WorkflowPlugin().ConfigureServices(services);

        var sink = new CaptureSink();
        services.AddSingleton(sink);
        services.AddScoped<IEventHandler<WorkflowActionRequested>, CapturingHandler<WorkflowActionRequested>>();

        var provider = services.BuildServiceProvider();
        var bus = provider.GetRequiredService<IEventBus>();

        await bus.PublishAsync(new ItemReorderPointDefined
        {
            Tenant = "acme",
            WarehouseId = "wh-1",
            Sku = "SKU-1",
            ReorderPoint = 10m,
        });
        await bus.PublishAsync(new StockMovementRecorded
        {
            Tenant = "acme",
            WarehouseId = "wh-1",
            Sku = "SKU-1",
            QuantityDelta = 12m, // 0 → 12, above the point
            OccurredAt = DateTimeOffset.UnixEpoch,
        });
        await bus.PublishAsync(new StockMovementRecorded
        {
            Tenant = "acme",
            WarehouseId = "wh-1",
            Sku = "SKU-1",
            QuantityDelta = -4m, // 12 → 8, crosses down
            OccurredAt = DateTimeOffset.UnixEpoch.AddHours(1),
        });

        var action = Assert.Single(sink.Events.OfType<WorkflowActionRequested>());
        Assert.Equal("PurchaseRequisitionRaised", action.TriggerType);
        Assert.Equal("Notify", action.Action);
        Assert.Equal("procurement", action.Channel);
        Assert.Contains("SKU-1", action.Subject, StringComparison.Ordinal);
        Assert.Contains("LowStock", action.Subject, StringComparison.Ordinal);
    }
}
