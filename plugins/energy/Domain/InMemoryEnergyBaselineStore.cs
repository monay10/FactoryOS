using System.Collections.Concurrent;

namespace FactoryOS.Plugins.Energy.Domain;

/// <summary>
/// The default in-memory <see cref="IEnergyBaselineStore"/>: a bounded rolling window of recent values per
/// aggregate. Thread-safe; each aggregate's window is guarded independently so concurrent meters never block
/// one another. Replaceable by a Redis-backed store behind the interface for horizontal scale.
/// </summary>
public sealed class InMemoryEnergyBaselineStore : IEnergyBaselineStore
{
    private readonly ConcurrentDictionary<EnergyMeterKey, Window> _windows = new();
    private readonly int _windowSize;

    /// <summary>Initializes a new instance of the <see cref="InMemoryEnergyBaselineStore"/> class.</summary>
    /// <param name="windowSize">How many recent readings the baseline averages over. Must be positive.</param>
    public InMemoryEnergyBaselineStore(int windowSize)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(windowSize);
        _windowSize = windowSize;
    }

    /// <inheritdoc />
    public BaselineSnapshot Observe(EnergyMeterKey key, decimal value)
    {
        ArgumentNullException.ThrowIfNull(key);
        var window = _windows.GetOrAdd(key, static _ => new Window());
        return window.Observe(value, _windowSize);
    }

    private sealed class Window
    {
        private readonly Queue<decimal> _values = new();
        private decimal _sum;

        public BaselineSnapshot Observe(decimal value, int windowSize)
        {
            lock (_values)
            {
                var priorCount = _values.Count;
                var priorAverage = priorCount > 0 ? _sum / priorCount : 0m;

                _values.Enqueue(value);
                _sum += value;
                while (_values.Count > windowSize)
                {
                    _sum -= _values.Dequeue();
                }

                return new BaselineSnapshot(priorCount, priorAverage);
            }
        }
    }
}
