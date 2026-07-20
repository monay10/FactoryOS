using FactoryOS.Contracts.Events;
using FactoryOS.Plugins.Production.Domain;

namespace FactoryOS.Plugins.Production.Application;

/// <summary>
/// The Production module's consumer of <see cref="ProductionOrderReleased"/>. It opens progress tracking for
/// the order. Registration is idempotent through the store, so a redelivered release neither resets progress
/// nor duplicates the order. It references no other module — only the shared event vocabulary.
/// </summary>
public sealed class ProductionOrderReleasedHandler : IEventHandler<ProductionOrderReleased>
{
    private readonly IProductionOrderStore _store;

    /// <summary>Initializes a new instance of the <see cref="ProductionOrderReleasedHandler"/> class.</summary>
    /// <param name="store">The production-order progress store.</param>
    public ProductionOrderReleasedHandler(IProductionOrderStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        _store = store;
    }

    /// <inheritdoc />
    public Task HandleAsync(
        ProductionOrderReleased integrationEvent,
        EventContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        ArgumentNullException.ThrowIfNull(context);

        _store.TryRegister(
            new ProductionOrderKey(integrationEvent.Tenant, integrationEvent.OrderId),
            integrationEvent.ProductId,
            integrationEvent.TargetQuantity);

        return Task.CompletedTask;
    }
}
