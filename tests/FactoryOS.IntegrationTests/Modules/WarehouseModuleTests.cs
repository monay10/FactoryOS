using System.Collections.Concurrent;
using FactoryOS.Contracts.Events;
using FactoryOS.Plugins.Warehouse;
using Microsoft.Extensions.DependencyInjection;

namespace FactoryOS.IntegrationTests.Modules;

/// <summary>
/// Proves the Warehouse module works event-driven through the real in-process event bus: a defined reorder point
/// and stock movements, published on the bus, are consumed by the plugin's handlers, which publish a
/// <see cref="LowStockDetected"/> back on the downward crossing — no module referencing another, only the bus.
/// </summary>
public sealed class WarehouseModuleTests
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
    public async Task A_movement_crossing_the_reorder_point_yields_a_low_stock_alert()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEventBus();
        new WarehousePlugin().ConfigureServices(services);

        var sink = new CaptureSink();
        services.AddSingleton(sink);
        services.AddScoped<IEventHandler<LowStockDetected>, CapturingHandler<LowStockDetected>>();

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
            QuantityDelta = 12m,
            OccurredAt = DateTimeOffset.UnixEpoch,
        });

        Assert.Empty(sink.Events.OfType<LowStockDetected>()); // above the point

        await bus.PublishAsync(new StockMovementRecorded
        {
            Tenant = "acme",
            WarehouseId = "wh-1",
            Sku = "SKU-1",
            QuantityDelta = -5m,
            OccurredAt = DateTimeOffset.UnixEpoch.AddHours(1),
        });

        var alert = Assert.Single(sink.Events.OfType<LowStockDetected>());
        Assert.Equal("SKU-1", alert.Sku);
        Assert.Equal(7m, alert.OnHand);
    }
}
