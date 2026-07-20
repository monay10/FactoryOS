using System.Collections.Concurrent;

namespace FactoryOS.Plugins.Quality.Domain;

/// <summary>
/// The default in-memory <see cref="IDefectRateWindowStore"/>: a bounded rolling window of recent inspections
/// per aggregate. Thread-safe; each aggregate's window is guarded independently so concurrent lines never block
/// one another. Unlike a single-value baseline, the returned aggregate includes the just-folded inspection, so
/// the current defective batch counts toward the rate the evaluator sees.
/// </summary>
public sealed class InMemoryDefectRateWindowStore : IDefectRateWindowStore
{
    private readonly ConcurrentDictionary<QualityLineKey, Window> _windows = new();
    private readonly int _windowSize;

    /// <summary>Initializes a new instance of the <see cref="InMemoryDefectRateWindowStore"/> class.</summary>
    /// <param name="windowSize">How many recent inspections the rolling rate spans. Must be positive.</param>
    public InMemoryDefectRateWindowStore(int windowSize)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(windowSize);
        _windowSize = windowSize;
    }

    /// <inheritdoc />
    public DefectRateSnapshot Fold(QualityLineKey key, int inspectedUnits, int defectiveUnits)
    {
        ArgumentNullException.ThrowIfNull(key);
        var window = _windows.GetOrAdd(key, static _ => new Window());
        return window.Fold(inspectedUnits, defectiveUnits, _windowSize);
    }

    /// <inheritdoc />
    public IReadOnlyCollection<QualityLineSnapshot> ForTenant(string tenant)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);
        return _windows
            .Where(pair => string.Equals(pair.Key.Tenant, tenant, StringComparison.Ordinal))
            .Select(pair => new QualityLineSnapshot(pair.Key.Tenant, pair.Key.LineId, pair.Key.ProductId, pair.Value.Current()))
            .ToList();
    }

    private sealed class Window
    {
        private readonly Queue<(int Inspected, int Defective)> _samples = new();
        private int _inspectedSum;
        private int _defectiveSum;

        public DefectRateSnapshot Fold(int inspectedUnits, int defectiveUnits, int windowSize)
        {
            lock (_samples)
            {
                _samples.Enqueue((inspectedUnits, defectiveUnits));
                _inspectedSum += inspectedUnits;
                _defectiveSum += defectiveUnits;
                while (_samples.Count > windowSize)
                {
                    var (droppedInspected, droppedDefective) = _samples.Dequeue();
                    _inspectedSum -= droppedInspected;
                    _defectiveSum -= droppedDefective;
                }

                return new DefectRateSnapshot(_inspectedSum, _defectiveSum);
            }
        }

        public DefectRateSnapshot Current()
        {
            lock (_samples)
            {
                return new DefectRateSnapshot(_inspectedSum, _defectiveSum);
            }
        }
    }
}
