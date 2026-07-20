using System.Collections.Concurrent;
using FactoryOS.Contracts.Events;
using FactoryOS.Plugins.Procurement;
using FactoryOS.Plugins.Warehouse;
using Microsoft.Extensions.DependencyInjection;

namespace FactoryOS.IntegrationTests.Modules;

/// <summary>
/// The showpiece cross-module test: a stock movement consumed by the Warehouse module crosses an item's reorder
/// point, which — purely over the real event bus — the Procurement module turns into a purchase requisition.
/// Both plugins are registered side by side; neither references the other. This proves Law 4 (event-driven only,
/// no module-to-module references) end to end.
/// </summary>
public sealed class WarehouseToProcurementChainTests
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
    public async Task A_low_stock_crossing_flows_from_warehouse_to_a_procurement_requisition()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEventBus();
        new WarehousePlugin().ConfigureServices(services);
        new ProcurementPlugin().ConfigureServices(services);

        var sink = new CaptureSink();
        services.AddSingleton(sink);
        services.AddScoped<IEventHandler<PurchaseRequisitionRaised>, CapturingHandler<PurchaseRequisitionRaised>>();

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

        Assert.Empty(sink.Events.OfType<PurchaseRequisitionRaised>());

        await bus.PublishAsync(new StockMovementRecorded
        {
            Tenant = "acme",
            WarehouseId = "wh-1",
            Sku = "SKU-1",
            QuantityDelta = -4m, // 12 → 8, crosses down
            OccurredAt = DateTimeOffset.UnixEpoch.AddHours(1),
        });

        var raised = Assert.Single(sink.Events.OfType<PurchaseRequisitionRaised>());
        Assert.Equal("SKU-1", raised.Requisition.Sku);
        Assert.Equal(12m, raised.Requisition.RequestedQuantity); // 10×2 − 8
        Assert.Equal("LowStock", raised.Reason);
    }
}
