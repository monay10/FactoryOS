using FactoryOS.Contracts.Events;
using FactoryOS.Plugins.Procurement;
using FactoryOS.Plugins.Procurement.Application;
using FactoryOS.Plugins.Procurement.Domain;

namespace FactoryOS.Tests.Procurement;

public sealed class LowStockDetectedHandlerTests
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

    private sealed record Harness(LowStockDetectedHandler Handler, RecordingEventBus Bus, IPurchaseRequisitionStore Store);

    private static Harness Build()
    {
        var bus = new RecordingEventBus();
        var store = new InMemoryPurchaseRequisitionStore();
        return new Harness(new LowStockDetectedHandler(bus, store, new ProcurementOptions()), bus, store);
    }

    private static LowStockDetected Alert(Guid? id = null) => new()
    {
        EventId = id ?? Guid.NewGuid(),
        Tenant = "acme",
        WarehouseId = "wh-1",
        Sku = "SKU-1",
        OnHand = 8m,
        ReorderPoint = 10m,
        OccurredAt = DateTimeOffset.UnixEpoch,
    };

    private static EventContext Context(IIntegrationEvent e) =>
        new(Guid.NewGuid(), e.EventId, Guid.NewGuid(), null, "trace", EventPriority.Normal, 1, e.OccurredOnUtc);

    [Fact]
    public async Task Raises_a_requisition_sized_by_the_reorder_policy()
    {
        var h = Build();
        var alert = Alert();

        await h.Handler.HandleAsync(alert, Context(alert), CancellationToken.None);

        var raised = Assert.Single(h.Bus.Published.OfType<PurchaseRequisitionRaised>());
        Assert.Equal("SKU-1", raised.Requisition.Sku);
        Assert.Equal("wh-1", raised.Requisition.WarehouseId);
        Assert.Equal(12m, raised.Requisition.RequestedQuantity); // 10×2 − 8
        Assert.Equal("Draft", raised.Requisition.Status);
        Assert.Equal("LowStock", raised.Reason);
        Assert.Equal(alert.EventId, raised.SourceEventId);
        Assert.StartsWith("PR-", raised.Requisition.Number, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_requisition_number_is_deterministic_from_the_source_event()
    {
        var h = Build();
        var id = Guid.NewGuid();

        await h.Handler.HandleAsync(Alert(id), Context(Alert(id)), CancellationToken.None);

        var expected = $"PR-{id:N}"[..("PR".Length + 9)].ToUpperInvariant();
        Assert.Equal(expected, Assert.Single(h.Store.ForTenant("acme")).Number);
    }

    [Fact]
    public async Task Redelivery_of_the_same_alert_raises_only_one_requisition()
    {
        var h = Build();
        var alert = Alert();

        await h.Handler.HandleAsync(alert, Context(alert), CancellationToken.None);
        await h.Handler.HandleAsync(alert, Context(alert), CancellationToken.None); // same event id

        Assert.Single(h.Bus.Published.OfType<PurchaseRequisitionRaised>());
        Assert.Single(h.Store.ForTenant("acme"));
    }
}
