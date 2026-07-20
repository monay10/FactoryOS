using System.Collections.Concurrent;

namespace FactoryOS.Plugins.Safety.Domain;

/// <summary>
/// The default in-memory <see cref="IIncidentWindowStore"/>: a bounded rolling window of recent incidents per
/// site. Thread-safe; each site's window is guarded independently so concurrent sites never block one another.
/// The window holds only occurrence markers — the count is what frequency detection needs.
/// </summary>
public sealed class InMemoryIncidentWindowStore : IIncidentWindowStore
{
    private readonly ConcurrentDictionary<SafetySiteKey, Window> _windows = new();
    private readonly int _windowSize;

    /// <summary>Initializes a new instance of the <see cref="InMemoryIncidentWindowStore"/> class.</summary>
    /// <param name="windowSize">How many recent incidents the frequency count spans. Must be positive.</param>
    public InMemoryIncidentWindowStore(int windowSize)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(windowSize);
        _windowSize = windowSize;
    }

    /// <inheritdoc />
    public int Fold(SafetySiteKey key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return _windows.GetOrAdd(key, static _ => new Window()).Fold(_windowSize);
    }

    private sealed class Window
    {
        private int _count;

        public int Fold(int windowSize)
        {
            lock (this)
            {
                if (_count < windowSize)
                {
                    _count++;
                }

                return _count;
            }
        }
    }
}
