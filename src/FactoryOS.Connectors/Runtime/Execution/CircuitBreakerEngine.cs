using System.Collections.Concurrent;
using FactoryOS.Connectors.Runtime.Domain;
using FactoryOS.Domain.Abstractions;

namespace FactoryOS.Connectors.Runtime.Execution;

/// <summary>A circuit's state at one moment.</summary>
/// <param name="Key">The circuit key.</param>
/// <param name="State">Whether calls flow, are refused, or one trial is permitted.</param>
/// <param name="ConsecutiveFailures">How many failures have accumulated since the last success.</param>
/// <param name="OpenedUtc">When the circuit opened, when it is open.</param>
public sealed record CircuitSnapshot(
    string Key, CircuitState State, int ConsecutiveFailures, DateTimeOffset? OpenedUtc);

/// <summary>
/// Stops calling an external system that is failing.
/// <para>
/// The circuit is keyed per <b>instance and operation</b>, not per connector. One factory's Logo being down
/// must not stop another factory's, and a failing report export must not close the door on the stock read
/// that shares the same connector.
/// </para>
/// <para>
/// Only failures that say something about the remote system count. A refused permission or a malformed
/// request is the caller's problem; counting those would let one badly-written client cut a whole factory off
/// from its ERP.
/// </para>
/// </summary>
public sealed class CircuitBreakerEngine
{
    private sealed record CircuitEntry(CircuitState State, int Failures, DateTimeOffset? OpenedUtc);

    private readonly ConcurrentDictionary<string, CircuitEntry> _circuits = new(StringComparer.Ordinal);
    private readonly IDateTimeProvider _clock;

    /// <summary>Initializes a new instance of the <see cref="CircuitBreakerEngine"/> class.</summary>
    /// <param name="clock">The clock that decides when a break has elapsed.</param>
    public CircuitBreakerEngine(IDateTimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        _clock = clock;
    }

    /// <summary>Builds the key one circuit is tracked under.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <param name="instance">The instance key.</param>
    /// <param name="operation">The operation name.</param>
    /// <returns>The circuit key.</returns>
    public static string KeyFor(string tenant, string instance, string operation)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);
        ArgumentException.ThrowIfNullOrWhiteSpace(instance);
        ArgumentException.ThrowIfNullOrWhiteSpace(operation);
        return $"{tenant}|{instance}|{operation}";
    }

    /// <summary>
    /// Determines whether a call may be attempted, moving an expired open circuit to half-open as it does.
    /// </summary>
    /// <param name="key">The circuit key.</param>
    /// <param name="breaker">The breaker policy.</param>
    /// <returns><see langword="true"/> when the call may proceed.</returns>
    public bool TryEnter(string key, ConnectorCircuitBreaker breaker)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(breaker);

        if (!breaker.Enabled)
        {
            return true;
        }

        var now = _clock.UtcNow;
        var entry = _circuits.AddOrUpdate(
            key,
            _ => new CircuitEntry(CircuitState.Closed, 0, null),
            (_, current) => current.State == CircuitState.Open
                            && current.OpenedUtc is { } opened
                            && now >= opened + breaker.BreakDuration
                ? current with { State = CircuitState.HalfOpen }
                : current);

        return entry.State != CircuitState.Open;
    }

    /// <summary>Records that a call succeeded, closing the circuit and clearing its failures.</summary>
    /// <param name="key">The circuit key.</param>
    public void RecordSuccess(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        _circuits[key] = new CircuitEntry(CircuitState.Closed, 0, null);
    }

    /// <summary>
    /// Records that a call failed. A failure while half-open re-opens the circuit immediately: the trial call
    /// exists precisely to answer whether the remote system recovered, and it just said no.
    /// </summary>
    /// <param name="key">The circuit key.</param>
    /// <param name="breaker">The breaker policy.</param>
    /// <param name="error">Why the call failed.</param>
    public void RecordFailure(string key, ConnectorCircuitBreaker breaker, ConnectorError error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(breaker);
        ArgumentNullException.ThrowIfNull(error);

        if (!breaker.Enabled || !error.CountsAgainstHealth)
        {
            return;
        }

        var now = _clock.UtcNow;
        _circuits.AddOrUpdate(
            key,
            _ => Next(new CircuitEntry(CircuitState.Closed, 0, null), breaker, now),
            (_, current) => Next(current, breaker, now));
    }

    /// <summary>Reads a circuit's state without changing it.</summary>
    /// <param name="key">The circuit key.</param>
    /// <returns>The snapshot; a circuit never exercised reads as closed.</returns>
    public CircuitSnapshot Snapshot(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return _circuits.TryGetValue(key, out var entry)
            ? new CircuitSnapshot(key, entry.State, entry.Failures, entry.OpenedUtc)
            : new CircuitSnapshot(key, CircuitState.Closed, 0, null);
    }

    /// <summary>Reads every tracked circuit.</summary>
    /// <returns>The snapshots, ordered by key.</returns>
    public IReadOnlyList<CircuitSnapshot> All() =>
    [
        .. _circuits
            .Select(pair => new CircuitSnapshot(pair.Key, pair.Value.State, pair.Value.Failures, pair.Value.OpenedUtc))
            .OrderBy(snapshot => snapshot.Key, StringComparer.Ordinal),
    ];

    /// <summary>Closes a circuit by hand, for an operator who knows the remote system is back.</summary>
    /// <param name="key">The circuit key.</param>
    public void Reset(string key) => RecordSuccess(key);

    private static CircuitEntry Next(CircuitEntry current, ConnectorCircuitBreaker breaker, DateTimeOffset now)
    {
        if (current.State == CircuitState.HalfOpen)
        {
            return new CircuitEntry(CircuitState.Open, current.Failures + 1, now);
        }

        var failures = current.Failures + 1;
        return failures >= breaker.FailureThreshold
            ? new CircuitEntry(CircuitState.Open, failures, now)
            : new CircuitEntry(CircuitState.Closed, failures, null);
    }
}
