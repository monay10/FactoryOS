using System.Globalization;

namespace FactoryOS.Connectors.Runtime.Domain;

/// <summary>
/// What one invocation cost and where it went: which instance and operation, under which correlation, how
/// long it took, how many attempts it needed and how it ended. This is the record every observability port
/// is fed from — one shape, so a measurement and an audit line can never disagree about the same call.
/// </summary>
/// <param name="Tenant">The tenant the call was made in.</param>
/// <param name="Instance">The instance key.</param>
/// <param name="Definition">The definition key.</param>
/// <param name="Operation">The operation name.</param>
/// <param name="Correlation">The identifiers tying it to the work that caused it.</param>
public sealed record ConnectorTelemetry(
    string Tenant,
    string Instance,
    string Definition,
    string Operation,
    ConnectorCorrelation Correlation)
{
    /// <summary>Gets when the invocation started.</summary>
    public DateTimeOffset StartedUtc { get; init; }

    /// <summary>Gets how long it took.</summary>
    public TimeSpan Duration { get; init; }

    /// <summary>Gets how many attempts were made.</summary>
    public int Attempts { get; init; } = 1;

    /// <summary>Gets whether it succeeded.</summary>
    public bool Succeeded { get; init; }

    /// <summary>Gets whether the answer came from the cache.</summary>
    public bool FromCache { get; init; }

    /// <summary>Gets why it failed, when it did.</summary>
    public ConnectorError? Error { get; init; }

    /// <summary>Gets the outcome as a label.</summary>
    public string Outcome => Succeeded
        ? FromCache ? "cached" : "success"
        : Error?.Kind.ToString().ToLowerInvariant() ?? "failure";

    /// <summary>Describes the invocation on one line.</summary>
    /// <returns>The description.</returns>
    public string Describe() => string.Create(
        CultureInfo.InvariantCulture,
        $"{Tenant}/{Instance}.{Operation} → {Outcome} in {Duration.TotalMilliseconds:F1}ms over {Attempts} attempt(s)");
}

/// <summary>
/// A running tally of what one connector instance has done: invocations, outcomes, retries, cache hits,
/// refusals and total time. Counters are derived from telemetry, never set by hand, so they cannot drift
/// away from what actually happened.
/// </summary>
public sealed class ConnectorMetrics
{
    private readonly Lock _gate = new();
    private long _invocations;
    private long _successes;
    private long _failures;
    private long _retries;
    private long _cacheHits;
    private long _throttled;
    private long _circuitRefusals;
    private long _refusals;
    private long _elapsedTicks;

    /// <summary>Records what one invocation did.</summary>
    /// <param name="telemetry">The invocation's telemetry.</param>
    public void Observe(ConnectorTelemetry telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);

        lock (_gate)
        {
            _invocations++;
            _elapsedTicks += telemetry.Duration.Ticks;
            _retries += Math.Max(0, telemetry.Attempts - 1);

            if (telemetry.FromCache)
            {
                _cacheHits++;
            }

            if (telemetry.Succeeded)
            {
                _successes++;
                return;
            }

            _failures++;
            switch (telemetry.Error?.Kind)
            {
                case ConnectorErrorKind.Throttled:
                    _throttled++;
                    break;
                case ConnectorErrorKind.CircuitOpen:
                    _circuitRefusals++;
                    break;
                case ConnectorErrorKind.Forbidden or ConnectorErrorKind.Unauthorized:
                    _refusals++;
                    break;
                default:
                    break;
            }
        }
    }

    /// <summary>Reads the counters as a consistent snapshot.</summary>
    /// <returns>The snapshot.</returns>
    public ConnectorMetricsSnapshot Snapshot()
    {
        lock (_gate)
        {
            return new ConnectorMetricsSnapshot(
                _invocations,
                _successes,
                _failures,
                _retries,
                _cacheHits,
                _throttled,
                _circuitRefusals,
                _refusals,
                TimeSpan.FromTicks(_elapsedTicks));
        }
    }
}

/// <summary>A consistent reading of a <see cref="ConnectorMetrics"/> tally.</summary>
/// <param name="Invocations">How many invocations were made.</param>
/// <param name="Successes">How many succeeded.</param>
/// <param name="Failures">How many failed.</param>
/// <param name="Retries">How many attempts beyond the first were made.</param>
/// <param name="CacheHits">How many answers came from the cache.</param>
/// <param name="Throttled">How many were refused by a rate limit.</param>
/// <param name="CircuitRefusals">How many were refused by an open circuit.</param>
/// <param name="Refusals">How many were refused on authentication or authorization.</param>
/// <param name="Elapsed">The total time spent invoking.</param>
public sealed record ConnectorMetricsSnapshot(
    long Invocations,
    long Successes,
    long Failures,
    long Retries,
    long CacheHits,
    long Throttled,
    long CircuitRefusals,
    long Refusals,
    TimeSpan Elapsed)
{
    /// <summary>Gets the mean invocation duration, or zero when nothing has been invoked.</summary>
    public TimeSpan Average => Invocations == 0 ? TimeSpan.Zero : TimeSpan.FromTicks(Elapsed.Ticks / Invocations);

    /// <summary>Gets the share of invocations that succeeded, or one when nothing has been invoked.</summary>
    /// <remarks>
    /// Silence reads as healthy here only because this is a ratio of what happened, not a health verdict.
    /// Whether an instance that has done nothing is healthy is the health engine's question, not this one's.
    /// </remarks>
    public double SuccessRate => Invocations == 0 ? 1.0 : (double)Successes / Invocations;
}
