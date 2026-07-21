namespace FactoryOS.Plugins.Workflow.Monitoring.Domain;

/// <summary>
/// The stored history of one <see cref="MetricInstance"/>: its values, in the order they were measured.
/// <para>
/// Values are appended and, when retention comes due, dropped or replaced by a coarser roll-up. There is no
/// operation that edits a value in place — history is rewritten only at the resolution retention chose, never
/// one number at a time.
/// </para>
/// </summary>
public sealed class MetricSeries
{
    private readonly List<MetricValue> _values = [];
    private readonly Lock _gate = new();

    /// <summary>Initializes a new instance of the <see cref="MetricSeries"/> class.</summary>
    /// <param name="instance">The series identity.</param>
    /// <param name="kind">What kind of quantity the series carries.</param>
    public MetricSeries(MetricInstance instance, MetricKind kind)
    {
        ArgumentNullException.ThrowIfNull(instance);
        Instance = instance;
        Kind = kind;
    }

    /// <summary>Gets the series identity.</summary>
    public MetricInstance Instance { get; }

    /// <summary>Gets what kind of quantity the series carries.</summary>
    public MetricKind Kind { get; }

    /// <summary>Gets the owning tenant.</summary>
    public string Tenant => Instance.Tenant;

    /// <summary>Gets the metric definition key.</summary>
    public string MetricKey => Instance.MetricKey;

    /// <summary>Gets how many values the series holds.</summary>
    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _values.Count;
            }
        }
    }

    /// <summary>Appends a value, keeping the series ordered by time.</summary>
    /// <param name="value">The value to append.</param>
    public void Append(MetricValue value)
    {
        ArgumentNullException.ThrowIfNull(value);
        lock (_gate)
        {
            // Measurements normally arrive in order, so the common path is a plain append; an out-of-order
            // arrival (a late bridge, a replayed event) is placed rather than left to corrupt the ordering.
            if (_values.Count == 0 || _values[^1].TimestampUtc <= value.TimestampUtc)
            {
                _values.Add(value);
                return;
            }

            var index = _values.FindLastIndex(existing => existing.TimestampUtc <= value.TimestampUtc);
            _values.Insert(index + 1, value);
        }
    }

    /// <summary>Reads the values as a snapshot in time order.</summary>
    /// <returns>The values.</returns>
    public IReadOnlyList<MetricValue> Values()
    {
        lock (_gate)
        {
            return _values.ToArray();
        }
    }

    /// <summary>
    /// Reads the values falling inside a window, its start exclusive and its end <b>inclusive</b>.
    /// <para>
    /// The end has to be inclusive: almost every window a reader asks for ends at "now", and a measurement
    /// taken this instant would otherwise be invisible until the clock moved past it — so a dashboard would
    /// always be one tick behind the thing it is watching. Making the start exclusive in exchange keeps
    /// consecutive windows a clean partition, with no measurement counted in two of them.
    /// </para>
    /// </summary>
    /// <param name="fromUtc">The start of the window, exclusive.</param>
    /// <param name="toUtc">The end of the window, inclusive.</param>
    /// <returns>The values in the window, in time order.</returns>
    public IReadOnlyList<MetricValue> Window(DateTimeOffset fromUtc, DateTimeOffset toUtc)
    {
        lock (_gate)
        {
            return _values.Where(value => value.TimestampUtc > fromUtc && value.TimestampUtc <= toUtc).ToArray();
        }
    }

    /// <summary>Gets the most recently measured value, or <see langword="null"/> when the series is empty.</summary>
    /// <returns>The last value.</returns>
    public MetricValue? Latest()
    {
        lock (_gate)
        {
            return _values.Count == 0 ? null : _values[^1];
        }
    }

    /// <summary>Removes every value measured before an instant.</summary>
    /// <param name="cutoffUtc">The cutoff; values at or after it are kept.</param>
    /// <param name="limit">The maximum number of values to remove in this pass.</param>
    /// <returns>How many values were removed.</returns>
    public int RemoveBefore(DateTimeOffset cutoffUtc, int limit)
    {
        lock (_gate)
        {
            var removable = _values.Count(value => value.TimestampUtc < cutoffUtc);
            var removed = Math.Min(removable, limit);
            if (removed > 0)
            {
                _values.RemoveRange(0, removed);
            }

            return removed;
        }
    }

    /// <summary>
    /// Replaces every value measured before an instant with a coarser set — how a roll-up keeps the shape of
    /// old history without keeping every point of it.
    /// </summary>
    /// <param name="cutoffUtc">The cutoff; values at or after it are kept as they are.</param>
    /// <param name="replacements">The rolled-up values to put in their place.</param>
    /// <returns>How many raw values were replaced.</returns>
    public int ReplaceBefore(DateTimeOffset cutoffUtc, IReadOnlyList<MetricValue> replacements)
    {
        ArgumentNullException.ThrowIfNull(replacements);
        lock (_gate)
        {
            var kept = _values.Where(value => value.TimestampUtc >= cutoffUtc).ToArray();
            var replaced = _values.Count - kept.Length;
            _values.Clear();
            _values.AddRange(replacements.OrderBy(value => value.TimestampUtc));
            _values.AddRange(kept);
            return replaced;
        }
    }
}
