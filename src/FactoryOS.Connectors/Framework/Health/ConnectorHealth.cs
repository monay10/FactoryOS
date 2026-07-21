using System.Collections.Concurrent;
using FactoryOS.Connectors.Framework.Configuration;
using FactoryOS.Domain.Abstractions;
using Microsoft.Extensions.Options;

namespace FactoryOS.Connectors.Framework.Health;

/// <summary>The health status of a connector.</summary>
public enum ConnectorHealthStatus
{
    /// <summary>No health signal has been recorded yet.</summary>
    Unknown = 0,

    /// <summary>The connector is beating within its heartbeat window and has no recorded failures.</summary>
    Healthy = 1,

    /// <summary>The connector has recorded failures but has not yet crossed the failure threshold.</summary>
    Degraded = 2,

    /// <summary>The connector missed its heartbeat window or latched into failure.</summary>
    Unhealthy = 3,
}

/// <summary>A point-in-time health snapshot of a connector.</summary>
/// <param name="Key">The connector key.</param>
/// <param name="Status">The health status.</param>
/// <param name="Detail">An optional human-readable detail (e.g. the last failure reason).</param>
/// <param name="LastHeartbeatUtc">The last heartbeat instant, or <see langword="null"/> when none was recorded.</param>
public sealed record ConnectorHealth(
    string Key, ConnectorHealthStatus Status, string? Detail, DateTimeOffset? LastHeartbeatUtc);

/// <summary>The health probe a connector may implement so the platform can pull its status on demand.</summary>
public interface IConnectorHealthCheck
{
    /// <summary>Reports the connector's current health status.</summary>
    /// <returns>The status.</returns>
    ConnectorHealthStatus Check();
}

/// <summary>Carries the connector whose health changed.</summary>
public sealed class ConnectorHealthEventArgs : EventArgs
{
    /// <summary>Initializes a new instance of the <see cref="ConnectorHealthEventArgs"/> class.</summary>
    /// <param name="health">The connector health snapshot.</param>
    public ConnectorHealthEventArgs(ConnectorHealth health)
    {
        ArgumentNullException.ThrowIfNull(health);
        Health = health;
    }

    /// <summary>Gets the connector health snapshot.</summary>
    public ConnectorHealth Health { get; }
}

/// <summary>
/// Tracks connector health: heartbeats, failure detection (heartbeat staleness and a failure counter) and
/// recovery detection. Beating within the heartbeat window keeps a connector healthy; missing the window or
/// crossing the failure threshold marks it unhealthy; a recovery clears failures and raises
/// <see cref="Recovered"/>.
/// </summary>
public interface IConnectorHealthService
{
    /// <summary>Raised when a connector transitions from an unhealthy state back to healthy.</summary>
    event EventHandler<ConnectorHealthEventArgs>? Recovered;

    /// <summary>Records a heartbeat for a connector, clearing failure state and marking it healthy.</summary>
    /// <param name="key">The connector key.</param>
    void Heartbeat(string key);

    /// <summary>Records a failure for a connector, latching it unhealthy once the threshold is reached.</summary>
    /// <param name="key">The connector key.</param>
    /// <param name="reason">The failure reason.</param>
    void ReportFailure(string key, string reason);

    /// <summary>Clears a connector's failure state and, if it was unhealthy, raises <see cref="Recovered"/>.</summary>
    /// <param name="key">The connector key.</param>
    void ReportRecovery(string key);

    /// <summary>Gets the current health snapshot of a connector (heartbeat staleness is evaluated on read).</summary>
    /// <param name="key">The connector key.</param>
    /// <returns>The health snapshot.</returns>
    ConnectorHealth GetHealth(string key);

    /// <summary>Gets the health snapshots of every tracked connector.</summary>
    /// <returns>The snapshots.</returns>
    IReadOnlyCollection<ConnectorHealth> All();
}

/// <summary>Default in-memory <see cref="IConnectorHealthService"/>.</summary>
public sealed class ConnectorHealthService : IConnectorHealthService
{
    private sealed record State(DateTimeOffset? LastHeartbeatUtc, int Failures, bool Latched, string? Detail);

    private readonly ConcurrentDictionary<string, State> _states = new(StringComparer.OrdinalIgnoreCase);
    private readonly IDateTimeProvider _clock;
    private readonly ConnectorHealthOptions _options;

    /// <summary>Initializes a new instance of the <see cref="ConnectorHealthService"/> class.</summary>
    /// <param name="clock">The clock used for heartbeat windows.</param>
    /// <param name="options">The connector options carrying the health policy.</param>
    public ConnectorHealthService(IDateTimeProvider clock, IOptions<ConnectorOptions> options)
    {
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(options);

        _clock = clock;
        _options = options.Value.Health;
    }

    /// <inheritdoc />
    public event EventHandler<ConnectorHealthEventArgs>? Recovered;

    /// <inheritdoc />
    public void Heartbeat(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var wasUnhealthy = IsUnhealthy(key);
        _states[key] = new State(_clock.UtcNow, 0, false, null);

        if (wasUnhealthy)
        {
            RaiseRecovered(key);
        }
    }

    /// <inheritdoc />
    public void ReportFailure(string key, string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        var current = _states.TryGetValue(key, out var state) ? state : new State(null, 0, false, null);
        var failures = current.Failures + 1;
        var latched = failures >= _options.FailureThreshold;
        _states[key] = current with { Failures = failures, Latched = latched, Detail = reason };
    }

    /// <inheritdoc />
    public void ReportRecovery(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var wasUnhealthy = IsUnhealthy(key);
        var current = _states.TryGetValue(key, out var state) ? state : new State(_clock.UtcNow, 0, false, null);
        _states[key] = current with { Failures = 0, Latched = false, Detail = null };

        if (wasUnhealthy)
        {
            RaiseRecovered(key);
        }
    }

    /// <inheritdoc />
    public ConnectorHealth GetHealth(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return Evaluate(key, _states.TryGetValue(key, out var state) ? state : null);
    }

    /// <inheritdoc />
    public IReadOnlyCollection<ConnectorHealth> All() =>
        _states.Select(pair => Evaluate(pair.Key, pair.Value)).ToArray();

    private bool IsUnhealthy(string key) => GetHealth(key).Status == ConnectorHealthStatus.Unhealthy;

    private void RaiseRecovered(string key) =>
        Recovered?.Invoke(this, new ConnectorHealthEventArgs(GetHealth(key)));

    private ConnectorHealth Evaluate(string key, State? state)
    {
        if (state is null)
        {
            return new ConnectorHealth(key, ConnectorHealthStatus.Unknown, null, null);
        }

        if (state.Latched)
        {
            return new ConnectorHealth(key, ConnectorHealthStatus.Unhealthy, state.Detail, state.LastHeartbeatUtc);
        }

        if (state.LastHeartbeatUtc is { } last)
        {
            var deadline = last.AddSeconds(
                (long)_options.HeartbeatIntervalSeconds * _options.UnhealthyAfterMissedHeartbeats);
            if (_clock.UtcNow > deadline)
            {
                return new ConnectorHealth(key, ConnectorHealthStatus.Unhealthy, "Heartbeat timed out.", last);
            }
        }

        var status = state.Failures > 0 ? ConnectorHealthStatus.Degraded : ConnectorHealthStatus.Healthy;
        return new ConnectorHealth(key, status, state.Detail, state.LastHeartbeatUtc);
    }
}
