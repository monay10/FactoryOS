using System.Collections.Concurrent;

namespace FactoryOS.Plugins.Warehouse.Domain;

/// <summary>
/// The default in-memory <see cref="IStockLedger"/>: a per-aggregate on-hand level and reorder point. Thread-safe;
/// each aggregate is mutated under its own lock so concurrent SKUs never block one another. Replaceable by an
/// EF Core or Redis read-model behind the interface.
/// </summary>
public sealed class InMemoryStockLedger : IStockLedger
{
    private readonly ConcurrentDictionary<WarehouseStockKey, Entry> _entries = new();

    /// <inheritdoc />
    public StockChange Apply(WarehouseStockKey key, decimal quantityDelta)
    {
        ArgumentNullException.ThrowIfNull(key);
        return _entries.GetOrAdd(key, static _ => new Entry()).Apply(quantityDelta);
    }

    /// <inheritdoc />
    public void SetReorderPoint(WarehouseStockKey key, decimal reorderPoint)
    {
        ArgumentNullException.ThrowIfNull(key);
        _entries.GetOrAdd(key, static _ => new Entry()).SetReorderPoint(reorderPoint);
    }

    /// <inheritdoc />
    public decimal? GetReorderPoint(WarehouseStockKey key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return _entries.TryGetValue(key, out var entry) ? entry.GetReorderPoint() : null;
    }

    /// <inheritdoc />
    public IReadOnlyCollection<StockLevel> ForTenant(string tenant)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);
        return _entries
            .Where(pair => string.Equals(pair.Key.Tenant, tenant, StringComparison.Ordinal))
            .Select(pair => pair.Value.ToLevel(pair.Key))
            .ToList();
    }

    private sealed class Entry
    {
        private decimal _onHand;
        private decimal? _reorderPoint;

        public StockChange Apply(decimal quantityDelta)
        {
            lock (this)
            {
                var previous = _onHand;
                _onHand += quantityDelta;
                return new StockChange(previous, _onHand);
            }
        }

        public void SetReorderPoint(decimal reorderPoint)
        {
            lock (this)
            {
                _reorderPoint = reorderPoint;
            }
        }

        public decimal? GetReorderPoint()
        {
            lock (this)
            {
                return _reorderPoint;
            }
        }

        public StockLevel ToLevel(WarehouseStockKey key)
        {
            lock (this)
            {
                return new StockLevel(key.Tenant, key.WarehouseId, key.Sku, _onHand, _reorderPoint);
            }
        }
    }
}
