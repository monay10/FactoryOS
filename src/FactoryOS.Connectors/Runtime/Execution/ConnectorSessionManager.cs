using System.Collections.Concurrent;
using FactoryOS.Connectors.Runtime.Configuration;
using FactoryOS.Connectors.Runtime.Domain;
using FactoryOS.Domain.Abstractions;
using Microsoft.Extensions.Options;

namespace FactoryOS.Connectors.Runtime.Execution;

/// <summary>
/// Keeps one connection per tenant instance open across invocations, and lets it lapse when it stops being
/// used.
/// <para>
/// A session belongs to an <b>instance</b>, not a caller: opening a connection to an ERP is expensive, and
/// two operators in the same factory should share the factory's connection rather than each paying for one.
/// It follows that a session cannot leak across tenants — the identity it is filed under is the tenant's
/// instance identity, so there is no lookup that could return another factory's connection.
/// </para>
/// <para>
/// Expiry is evaluated on read rather than by a timer. A background sweep that has not run yet is a session
/// that is expired in principle and alive in practice, which is exactly the gap a stale connection lives in.
/// </para>
/// </summary>
public sealed class ConnectorSessionManager
{
    private readonly ConcurrentDictionary<string, ConnectorSession> _sessions = new(StringComparer.OrdinalIgnoreCase);
    private readonly IDateTimeProvider _clock;
    private readonly ConnectorRuntimeOptions _options;

    /// <summary>Initializes a new instance of the <see cref="ConnectorSessionManager"/> class.</summary>
    /// <param name="clock">The clock that decides when a session has lapsed.</param>
    /// <param name="options">The runtime options carrying the idle timeout.</param>
    public ConnectorSessionManager(IDateTimeProvider clock, IOptions<ConnectorRuntimeOptions> options)
    {
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(options);
        _clock = clock;
        _options = options.Value;
    }

    /// <summary>Gets the session for an instance, opening one when there is none or the last has lapsed.</summary>
    /// <param name="instance">The instance.</param>
    /// <returns>An active session.</returns>
    public ConnectorSession Acquire(ConnectorInstance instance)
    {
        ArgumentNullException.ThrowIfNull(instance);

        var now = _clock.UtcNow;
        var session = _sessions.AddOrUpdate(
            instance.Identity,
            _ => Open(instance, now),
            (_, current) =>
            {
                if (current.IsActive(now))
                {
                    return current;
                }

                current.Close(now);
                return Open(instance, now);
            });

        session.Touch(now);
        return session;
    }

    /// <summary>Finds an instance's session without opening one.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <param name="instanceKey">The instance key.</param>
    /// <returns>The session, or <see langword="null"/> when there is none or it has lapsed.</returns>
    public ConnectorSession? Find(string tenant, string instanceKey)
    {
        var identity = ConnectorInstance.Identify(tenant, instanceKey);
        if (!_sessions.TryGetValue(identity, out var session))
        {
            return null;
        }

        return session.IsActive(_clock.UtcNow) ? session : null;
    }

    /// <summary>Closes an instance's session.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <param name="instanceKey">The instance key.</param>
    /// <returns><see langword="true"/> when a session was closed.</returns>
    public bool Close(string tenant, string instanceKey)
    {
        var identity = ConnectorInstance.Identify(tenant, instanceKey);
        if (!_sessions.TryRemove(identity, out var session))
        {
            return false;
        }

        session.Close(_clock.UtcNow);
        return true;
    }

    /// <summary>Closes and forgets every session that has lapsed.</summary>
    /// <returns>How many sessions were reaped.</returns>
    public int Reap()
    {
        var now = _clock.UtcNow;
        var reaped = 0;

        foreach (var pair in _sessions.ToArray())
        {
            if (pair.Value.IsActive(now) || !_sessions.TryRemove(pair.Key, out var session))
            {
                continue;
            }

            session.Close(now);
            reaped++;
        }

        return reaped;
    }

    /// <summary>Lists one tenant's active sessions.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <returns>The sessions.</returns>
    public IReadOnlyList<ConnectorSession> ListByTenant(string tenant)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);

        var now = _clock.UtcNow;
        return
        [
            .. _sessions.Values
                .Where(session =>
                    string.Equals(session.Tenant, tenant, StringComparison.OrdinalIgnoreCase) && session.IsActive(now))
                .OrderBy(session => session.Instance, StringComparer.Ordinal),
        ];
    }

    private ConnectorSession Open(ConnectorInstance instance, DateTimeOffset now) =>
        new(Guid.NewGuid(), instance.Tenant, instance.Key, now, _options.SessionIdleTimeout);
}
