using System.Collections.Concurrent;

namespace FactoryOS.Plugins.Production.Domain;

/// <summary>
/// The default in-memory <see cref="IProductionOrderStore"/>: a per-tenant map of order state. Thread-safe;
/// each order's state is mutated under its own lock so concurrent orders never block one another. Replaceable
/// by an EF Core or Redis read-model behind the interface.
/// </summary>
public sealed class InMemoryProductionOrderStore : IProductionOrderStore
{
    private readonly ConcurrentDictionary<ProductionOrderKey, OrderState> _orders = new();
    private readonly bool _allowOverProduction;

    /// <summary>Initializes a new instance of the <see cref="InMemoryProductionOrderStore"/> class.</summary>
    /// <param name="allowOverProduction">Whether counts keep accruing past the target (otherwise capped at it).</param>
    public InMemoryProductionOrderStore(bool allowOverProduction)
    {
        _allowOverProduction = allowOverProduction;
    }

    /// <inheritdoc />
    public bool TryRegister(ProductionOrderKey key, string productId, int targetQuantity)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(productId);
        return _orders.TryAdd(key, new OrderState(productId, targetQuantity));
    }

    /// <inheritdoc />
    public AccrualResult Accrue(ProductionOrderKey key, int producedCount)
    {
        ArgumentNullException.ThrowIfNull(key);
        return _orders.TryGetValue(key, out var state)
            ? state.Accrue(producedCount, _allowOverProduction)
            : AccrualResult.NotFound;
    }

    /// <inheritdoc />
    public IReadOnlyCollection<ProductionOrderProgress> ForTenant(string tenant)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);
        return _orders
            .Where(pair => string.Equals(pair.Key.Tenant, tenant, StringComparison.Ordinal))
            .Select(pair => pair.Value.ToProgress(tenant, pair.Key.OrderId))
            .ToList();
    }

    private sealed class OrderState
    {
        private readonly string _productId;
        private readonly int _target;
        private int _produced;
        private bool _completed;

        public OrderState(string productId, int target)
        {
            _productId = productId;
            _target = target;
        }

        public AccrualResult Accrue(int producedCount, bool allowOverProduction)
        {
            lock (this)
            {
                if (_completed && !allowOverProduction)
                {
                    // Order is locked at target: ignore further counts.
                    return new AccrualResult(true, false, _productId, _target, _produced);
                }

                _produced += producedCount;
                if (!allowOverProduction && _produced > _target)
                {
                    _produced = _target;
                }

                var reachedTarget = _produced >= _target;
                var justCompleted = reachedTarget && !_completed;
                _completed = _completed || reachedTarget;

                return new AccrualResult(true, justCompleted, _productId, _target, _produced);
            }
        }

        public ProductionOrderProgress ToProgress(string tenant, string orderId)
        {
            lock (this)
            {
                return new ProductionOrderProgress(tenant, orderId, _productId, _target, _produced, _completed);
            }
        }
    }
}
