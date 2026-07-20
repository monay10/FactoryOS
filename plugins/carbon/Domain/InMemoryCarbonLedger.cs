using System.Collections.Concurrent;

namespace FactoryOS.Plugins.Carbon.Domain;

/// <summary>
/// The default in-memory <see cref="ICarbonLedger"/>: a per-source cumulative total. Thread-safe; each source's
/// total is mutated under its own lock so concurrent sources never block one another. Replaceable by an EF Core
/// or Redis read-model behind the interface.
/// </summary>
public sealed class InMemoryCarbonLedger : ICarbonLedger
{
    private readonly ConcurrentDictionary<CarbonSourceKey, Total> _totals = new();

    /// <inheritdoc />
    public decimal Accrue(CarbonSourceKey key, decimal co2eKg)
    {
        ArgumentNullException.ThrowIfNull(key);
        return _totals.GetOrAdd(key, static _ => new Total()).Accrue(co2eKg);
    }

    /// <inheritdoc />
    public IReadOnlyCollection<CarbonTotal> ForTenant(string tenant)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);
        return _totals
            .Where(pair => string.Equals(pair.Key.Tenant, tenant, StringComparison.Ordinal))
            .Select(pair => new CarbonTotal(tenant, pair.Key.Source, pair.Value.Current))
            .ToList();
    }

    private sealed class Total
    {
        private decimal _cumulative;

        public decimal Current
        {
            get
            {
                lock (this)
                {
                    return _cumulative;
                }
            }
        }

        public decimal Accrue(decimal co2eKg)
        {
            lock (this)
            {
                _cumulative += co2eKg;
                return _cumulative;
            }
        }
    }
}
