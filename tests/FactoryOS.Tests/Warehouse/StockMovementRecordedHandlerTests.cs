using FactoryOS.Contracts.Events;
using FactoryOS.Plugins.Warehouse;
using FactoryOS.Plugins.Warehouse.Application;
using FactoryOS.Plugins.Warehouse.Domain;

namespace FactoryOS.Tests.Warehouse;

public sealed class StockMovementRecordedHandlerTests
{
    private sealed class RecordingEventBus : IEventBus
    {
        public List<IIntegrationEvent> Published { get; } = [];

        public Task PublishAsync<TEvent>(TEvent integrationEvent, EventPublishOptions? options = null, CancellationToken cancellationToken = default)
            where TEvent : IIntegrationEvent
        {
            Published.Add(integrationEvent);
            return Task.CompletedTask;
        }
    }

    private sealed record Harness(
        StockMovementRecordedHandler Movement,
        ItemReorderPointDefinedHandler Reorder,
        RecordingEventBus Bus,
        IStockLedger Ledger);

    private static Harness Build(decimal defaultReorderPoint = 0m)
    {
        var bus = new RecordingEventBus();
        var ledger = new InMemoryStockLedger();
        var processed = new InMemoryProcessedEventLog();
        var options = new WarehouseOptions { DefaultReorderPoint = defaultReorderPoint };
        return new Harness(
            new StockMovementRecordedHandler(bus, ledger, processed, options),
            new ItemReorderPointDefinedHandler(ledger),
            bus,
            ledger);
    }

    private static EventContext Context(IIntegrationEvent e) =>
        new(Guid.NewGuid(), e.EventId, Guid.NewGuid(), null, "trace", EventPriority.Normal, 1, e.OccurredOnUtc);

    private static StockMovementRecorded Movement(decimal delta) => new()
    {
        Tenant = "acme",
        WarehouseId = "wh-1",
        Sku = "SKU-1",
        QuantityDelta = delta,
        OccurredAt = DateTimeOffset.UnixEpoch,
    };

    private static ItemReorderPointDefined Reorder(decimal point) => new()
    {
        Tenant = "acme",
        WarehouseId = "wh-1",
        Sku = "SKU-1",
        ReorderPoint = point,
    };

    private static async Task Move(Harness h, StockMovementRecorded m) =>
        await h.Movement.HandleAsync(m, Context(m), CancellationToken.None);

    private static async Task Define(Harness h, ItemReorderPointDefined r) =>
        await h.Reorder.HandleAsync(r, Context(r), CancellationToken.None);

    [Fact]
    public async Task Raises_low_stock_when_a_movement_crosses_the_reorder_point()
    {
        var h = Build();
        await Define(h, Reorder(10m));
        await Move(h, Movement(12m));  // 0 → 12, above
        Assert.Empty(h.Bus.Published);

        await Move(h, Movement(-4m));  // 12 → 8, crosses down

        var alert = Assert.Single(h.Bus.Published.OfType<LowStockDetected>());
        Assert.Equal("SKU-1", alert.Sku);
        Assert.Equal(8m, alert.OnHand);
        Assert.Equal(10m, alert.ReorderPoint);
    }

    [Fact]
    public async Task Does_not_refire_while_still_below()
    {
        var h = Build();
        await Define(h, Reorder(10m));
        await Move(h, Movement(12m));
        await Move(h, Movement(-4m)); // crosses down → 1 alert
        await Move(h, Movement(-2m)); // 8 → 6, still below

        Assert.Single(h.Bus.Published.OfType<LowStockDetected>());
    }

    [Fact]
    public async Task Uses_the_default_reorder_point_when_none_is_defined()
    {
        var h = Build(defaultReorderPoint: 5m);
        await Move(h, Movement(8m));   // 0 → 8, above default 5
        await Move(h, Movement(-4m));  // 8 → 4, crosses default

        Assert.Single(h.Bus.Published.OfType<LowStockDetected>());
    }

    [Fact]
    public async Task Stays_silent_without_any_reorder_point()
    {
        var h = Build(); // default 0 → disabled
        await Move(h, Movement(10m));
        await Move(h, Movement(-9m));

        Assert.Empty(h.Bus.Published);
    }

    [Fact]
    public async Task Redelivery_of_the_same_movement_is_not_double_applied()
    {
        var h = Build();
        await Define(h, Reorder(10m));
        var movement = Movement(12m);

        await Move(h, movement);
        await Move(h, movement); // same event id

        Assert.Equal(12m, Assert.Single(h.Ledger.ForTenant("acme")).OnHand); // not 24
    }
}
