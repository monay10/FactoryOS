namespace FactoryOS.Plugins.Workflow.Monitoring.Domain;

/// <summary>
/// One name-value pair qualifying a measurement — the definition key a workflow duration belongs to, the
/// channel a notification went out on, the outcome of an operation.
/// </summary>
/// <param name="Name">The label name.</param>
/// <param name="Value">The label value.</param>
public sealed record MetricLabel(string Name, string Value)
{
    /// <summary>Creates a label, normalising the name so labels never differ by case alone.</summary>
    /// <param name="name">The label name.</param>
    /// <param name="value">The label value.</param>
    /// <returns>The label.</returns>
    public static MetricLabel Of(string name, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(value);
        return new MetricLabel(name.Trim().ToLowerInvariant(), value);
    }
}

/// <summary>
/// The set of labels that separates one series of a metric from another. Two measurements of the same metric
/// with the same dimension belong to the same series; a different dimension is a different series.
/// <para>
/// The dimension is an immutable value object with a stable, ordinally sorted <see cref="Key"/>, which is what
/// series identity is built on — so labels supplied in a different order still land in the same series rather
/// than silently splitting one series into two.
/// </para>
/// </summary>
public sealed class MetricDimension : IEquatable<MetricDimension>
{
    private readonly Dictionary<string, string> _labels;

    /// <summary>Initializes a new instance of the <see cref="MetricDimension"/> class.</summary>
    /// <param name="labels">The labels qualifying the series.</param>
    public MetricDimension(IEnumerable<MetricLabel>? labels = null)
    {
        _labels = new Dictionary<string, string>(StringComparer.Ordinal);
        if (labels is null)
        {
            Key = string.Empty;
            return;
        }

        foreach (var label in labels)
        {
            ArgumentNullException.ThrowIfNull(label);
            _labels[label.Name] = label.Value;
        }

        Key = Render(_labels);
    }

    /// <summary>A dimension carrying no labels — the whole metric, unsliced.</summary>
    public static MetricDimension None { get; } = new();

    /// <summary>Gets the labels, keyed by name.</summary>
    public IReadOnlyDictionary<string, string> Labels => _labels;

    /// <summary>
    /// Gets the canonical, ordinally sorted rendering of the labels. Series identity is this string, so it
    /// never varies with the order labels were supplied in.
    /// </summary>
    public string Key { get; }

    /// <summary>Gets the value of a label, or <see langword="null"/> when it is not present.</summary>
    /// <param name="name">The label name.</param>
    /// <returns>The value, or <see langword="null"/>.</returns>
    public string? this[string name] => _labels.TryGetValue(name, out var value) ? value : null;

    /// <summary>Creates a dimension from name-value pairs.</summary>
    /// <param name="labels">The labels.</param>
    /// <returns>The dimension.</returns>
    public static MetricDimension Of(params MetricLabel[] labels) => new(labels);

    /// <summary>Returns a copy carrying an additional label.</summary>
    /// <param name="name">The label name.</param>
    /// <param name="value">The label value.</param>
    /// <returns>A new instance; this one is unchanged.</returns>
    public MetricDimension With(string name, string value) =>
        new(_labels.Select(pair => new MetricLabel(pair.Key, pair.Value)).Append(MetricLabel.Of(name, value)));

    /// <summary>
    /// Gets a value indicating whether this dimension carries every label of another — the test a search uses
    /// to match a filter against a series without demanding an exact label set.
    /// </summary>
    /// <param name="other">The labels that must all be present.</param>
    /// <returns><see langword="true"/> when every label of <paramref name="other"/> matches.</returns>
    public bool Covers(MetricDimension other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return other._labels.All(pair =>
            _labels.TryGetValue(pair.Key, out var value) && string.Equals(value, pair.Value, StringComparison.Ordinal));
    }

    /// <inheritdoc />
    public bool Equals(MetricDimension? other) =>
        other is not null && string.Equals(Key, other.Key, StringComparison.Ordinal);

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as MetricDimension);

    /// <inheritdoc />
    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Key);

    /// <inheritdoc />
    public override string ToString() => Key;

    private static string Render(Dictionary<string, string> labels) => string.Join(
        ',', labels.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => $"{pair.Key}={pair.Value}"));
}

/// <summary>
/// The identifiers tying a measurement back to the operation that produced it. They are carried through
/// verbatim from the source event: an alert is far more useful when it can name the trace that tripped it than
/// when it can only name the number.
/// </summary>
/// <param name="CorrelationId">Groups every measurement produced by one logical operation.</param>
/// <param name="TraceId">The distributed trace the operation belonged to.</param>
/// <param name="RequestId">The inbound request that triggered the operation.</param>
public sealed record MetricCorrelation(
    string? CorrelationId = null, string? TraceId = null, string? RequestId = null)
{
    /// <summary>An empty correlation, for measurements with no ambient context.</summary>
    public static MetricCorrelation None { get; } = new();

    /// <summary>Gets a value indicating whether any identifier is present.</summary>
    public bool IsEmpty => CorrelationId is null && TraceId is null && RequestId is null;

    /// <summary>Creates a correlation that only groups by a logical operation id.</summary>
    /// <param name="correlationId">The correlation id.</param>
    /// <returns>The correlation.</returns>
    public static MetricCorrelation For(string correlationId) => new(correlationId);
}
