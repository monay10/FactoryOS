using System.Collections.Concurrent;
using FactoryOS.Domain.Abstractions;
using FactoryOS.Plugin.Configuration;
using Microsoft.Extensions.Options;

namespace FactoryOS.Plugin.Health;

/// <summary>The health status of a plugin.</summary>
public enum PluginHealthStatus
{
    /// <summary>No health signal has been recorded yet.</summary>
    Unknown = 0,

    /// <summary>The plugin is beating within its heartbeat window and has no recorded failures.</summary>
    Healthy = 1,

    /// <summary>The plugin has recorded failures but has not yet crossed the failure threshold.</summary>
    Degraded = 2,

    /// <summary>The plugin missed its heartbeat window or latched into failure.</summary>
    Unhealthy = 3,
}

/// <summary>A point-in-time health snapshot of a plugin.</summary>
/// <param name="Key">The plugin key.</param>
/// <param name="Status">The health status.</param>
/// <param name="Detail">An optional human-readable detail (e.g. the last failure reason).</param>
/// <param name="LastHeartbeatUtc">The last heartbeat instant, or <see langword="null"/> when none was recorded.</param>
public sealed record PluginHealth(
    string Key, PluginHealthStatus Status, string? Detail, DateTimeOffset? LastHeartbeatUtc);

/// <summary>The health probe a plugin may implement so the framework can pull its status on demand.</summary>
public interface IPluginHealthCheck
{
    /// <summary>Reports the plugin's current health status.</summary>
    /// <returns>The status.</returns>
    PluginHealthStatus Check();
}

/// <summary>Carries the plugin whose health changed.</summary>
public sealed class PluginHealthEventArgs : EventArgs
{
    /// <summary>Initializes a new instance of the <see cref="PluginHealthEventArgs"/> class.</summary>
    /// <param name="health">The plugin health snapshot.</param>
    public PluginHealthEventArgs(PluginHealth health)
    {
        ArgumentNullException.ThrowIfNull(health);
        Health = health;
    }

    /// <summary>Gets the plugin health snapshot.</summary>
    public PluginHealth Health { get; }
}

/// <summary>
/// Tracks plugin health: heartbeats, failure detection (heartbeat staleness and a failure counter) and
/// recovery notification. Beating within the heartbeat window keeps a plugin healthy; missing the window
/// or crossing the failure threshold marks it unhealthy; a recovery clears failures and raises
/// <see cref="Recovered"/>.
/// </summary>
public interface IPluginHealthService
{
    /// <summary>Raised when a plugin transitions from an unhealthy/degraded state back to healthy.</summary>
    event EventHandler<PluginHealthEventArgs>? Recovered;

    /// <summary>Records a heartbeat for a plugin, clearing failure state and marking it healthy.</summary>
    /// <param name="key">The plugin key.</param>
    void Heartbeat(string key);

    /// <summary>Records a failure for a plugin, latching it unhealthy once the threshold is reached.</summary>
    /// <param name="key">The plugin key.</param>
    /// <param name="reason">The failure reason.</param>
    void ReportFailure(string key, string reason);

    /// <summary>Clears a plugin's failure state and, if it was unhealthy, raises <see cref="Recovered"/>.</summary>
    /// <param name="key">The plugin key.</param>
    void ReportRecovery(string key);

    /// <summary>Gets the current health snapshot of a plugin (heartbeat staleness is evaluated on read).</summary>
    /// <param name="key">The plugin key.</param>
    /// <returns>The health snapshot.</returns>
    PluginHealth GetHealth(string key);

    /// <summary>Gets the health snapshots of every tracked plugin.</summary>
    /// <returns>The snapshots.</returns>
    IReadOnlyCollection<PluginHealth> All();
}

/// <summary>Default in-memory <see cref="IPluginHealthService"/>.</summary>
public sealed class PluginHealthService : IPluginHealthService
{
    private sealed record State(DateTimeOffset? LastHeartbeatUtc, int Failures, bool Latched, string? Detail);

    private readonly ConcurrentDictionary<string, State> _states = new(StringComparer.OrdinalIgnoreCase);
    private readonly IDateTimeProvider _clock;
    private readonly PluginHealthOptions _options;

    /// <summary>Initializes a new instance of the <see cref="PluginHealthService"/> class.</summary>
    /// <param name="clock">The clock used for heartbeat windows.</param>
    /// <param name="options">The plugin options carrying the health policy.</param>
    public PluginHealthService(IDateTimeProvider clock, IOptions<PluginOptions> options)
    {
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(options);

        _clock = clock;
        _options = options.Value.Health;
    }

    /// <inheritdoc />
    public event EventHandler<PluginHealthEventArgs>? Recovered;

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
    public PluginHealth GetHealth(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return Evaluate(key, _states.TryGetValue(key, out var state) ? state : null);
    }

    /// <inheritdoc />
    public IReadOnlyCollection<PluginHealth> All() =>
        _states.Select(pair => Evaluate(pair.Key, pair.Value)).ToArray();

    private bool IsUnhealthy(string key) => GetHealth(key).Status == PluginHealthStatus.Unhealthy;

    private void RaiseRecovered(string key) =>
        Recovered?.Invoke(this, new PluginHealthEventArgs(GetHealth(key)));

    private PluginHealth Evaluate(string key, State? state)
    {
        if (state is null)
        {
            return new PluginHealth(key, PluginHealthStatus.Unknown, null, null);
        }

        if (state.Latched)
        {
            return new PluginHealth(key, PluginHealthStatus.Unhealthy, state.Detail, state.LastHeartbeatUtc);
        }

        if (state.LastHeartbeatUtc is { } last)
        {
            var deadline = last.AddSeconds(
                (long)_options.HeartbeatIntervalSeconds * _options.UnhealthyAfterMissedHeartbeats);
            if (_clock.UtcNow > deadline)
            {
                return new PluginHealth(key, PluginHealthStatus.Unhealthy, "Heartbeat timed out.", last);
            }
        }

        var status = state.Failures > 0 ? PluginHealthStatus.Degraded : PluginHealthStatus.Healthy;
        return new PluginHealth(key, status, state.Detail, state.LastHeartbeatUtc);
    }
}
