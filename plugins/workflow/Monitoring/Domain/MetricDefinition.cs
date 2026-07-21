namespace FactoryOS.Plugins.Workflow.Monitoring.Domain;

/// <summary>
/// What a metric <i>is</i>: its stable key, what it measures, and how it may be read. A definition is
/// registered once per platform and is the same for every tenant — tenants differ in the values they produce,
/// never in what a metric means.
/// </summary>
public sealed record MetricDefinition
{
    private readonly double _sampleRate = 1.0;

    /// <summary>Initializes a new instance of the <see cref="MetricDefinition"/> record.</summary>
    /// <param name="key">The stable, dotted metric key.</param>
    /// <param name="category">Which part of the platform the metric describes.</param>
    /// <param name="kind">What kind of quantity it carries.</param>
    /// <param name="unit">The unit values are expressed in.</param>
    /// <param name="description">What the metric means.</param>
    public MetricDefinition(string key, MetricCategory category, MetricKind kind, string unit, string description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(unit);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        Key = key;
        Category = category;
        Kind = kind;
        Unit = unit;
        Description = description;
    }

    /// <summary>Gets the stable, dotted metric key (for example <c>workflow.instance.completed</c>).</summary>
    public string Key { get; }

    /// <summary>Gets which part of the platform the metric describes.</summary>
    public MetricCategory Category { get; }

    /// <summary>Gets what kind of quantity the metric carries.</summary>
    public MetricKind Kind { get; }

    /// <summary>Gets the unit values are expressed in (<c>count</c>, <c>ms</c>, <c>bytes</c>, …).</summary>
    public string Unit { get; }

    /// <summary>Gets what the metric means.</summary>
    public string Description { get; }

    /// <summary>
    /// Gets the aggregation used when a reader asks for none. It follows from the kind rather than being
    /// configured separately: summing a gauge or averaging a counter are both meaningless, and a default that
    /// contradicts the kind would be a bug waiting for a dashboard to reveal it.
    /// </summary>
    public MetricAggregation DefaultAggregation => Kind switch
    {
        MetricKind.Counter => MetricAggregation.Sum,
        MetricKind.Gauge => MetricAggregation.Last,
        _ => MetricAggregation.Average,
    };

    /// <summary>
    /// Gets the fraction of samples kept for this metric, between zero (exclusive) and one. One keeps every
    /// sample; a smaller value hands the metric to the sampler. A rate of zero would mean "measure this, but
    /// never record it", which is never what anybody means, so it is rejected rather than quietly honoured.
    /// </summary>
    public double SampleRate
    {
        get => _sampleRate;
        init
        {
            if (value is <= 0 or > 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value), value, "A sample rate must be greater than zero and at most one.");
            }

            _sampleRate = value;
        }
    }

    /// <summary>Gets the label names this metric is expected to be sliced by.</summary>
    public IReadOnlyList<string> Dimensions { get; init; } = [];
}
