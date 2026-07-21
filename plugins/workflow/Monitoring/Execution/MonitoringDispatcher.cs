using System.Collections.Concurrent;
using FactoryOS.Plugins.Workflow.Monitoring.Domain;
using FactoryOS.Plugins.Workflow.Monitoring.Events;

namespace FactoryOS.Plugins.Workflow.Monitoring.Execution;

/// <summary>
/// Publishes monitoring events to every registered sink. The fan-out is deliberate: an exporter, a live
/// dashboard feed and an alert forwarder all legitimately watch the same stream, and none of them should be
/// able to displace another by being registered second.
/// </summary>
public sealed class MonitoringDispatcher
{
    private readonly IEnumerable<IMonitoringEventSink> _sinks;

    /// <summary>Initializes a new instance of the <see cref="MonitoringDispatcher"/> class.</summary>
    /// <param name="sinks">The sinks to fan out to.</param>
    public MonitoringDispatcher(IEnumerable<IMonitoringEventSink> sinks)
    {
        ArgumentNullException.ThrowIfNull(sinks);
        _sinks = sinks;
    }

    /// <summary>Publishes an event to every sink.</summary>
    /// <param name="monitoringEvent">The event.</param>
    public void Publish(MonitoringEvent monitoringEvent)
    {
        ArgumentNullException.ThrowIfNull(monitoringEvent);
        foreach (var sink in _sinks)
        {
            sink.Publish(monitoringEvent);
        }
    }
}

/// <summary>Holds the monitoring rights granted to principals.</summary>
public interface IMonitoringPermissionStore
{
    /// <summary>Grants rights to a principal (grants accumulate).</summary>
    /// <param name="principal">The principal (a user id, <c>role:x</c> or <c>group:x</c>).</param>
    /// <param name="permission">The rights to grant.</param>
    void Grant(string principal, MonitoringPermission permission);

    /// <summary>Gets the rights granted to a principal.</summary>
    /// <param name="principal">The principal.</param>
    /// <returns>The accumulated rights.</returns>
    MonitoringPermission Granted(string principal);
}

/// <summary>An in-memory <see cref="IMonitoringPermissionStore"/>.</summary>
public sealed class InMemoryMonitoringPermissionStore : IMonitoringPermissionStore
{
    private readonly ConcurrentDictionary<string, MonitoringPermission> _grants = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public void Grant(string principal, MonitoringPermission permission)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(principal);
        _grants.AddOrUpdate(principal, permission, (_, existing) => existing | permission);
    }

    /// <inheritdoc />
    public MonitoringPermission Granted(string principal)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(principal);
        return _grants.TryGetValue(principal, out var permission) ? permission : MonitoringPermission.None;
    }
}

/// <summary>
/// Decides what a principal may do with the monitoring surface. Nothing is implicit: metrics say how much work
/// a factory did and where it went wrong, which is exactly the sort of thing a tenant expects not to be
/// readable by anyone who happens to know the URL.
/// </summary>
public sealed class MonitoringPermissionEvaluator
{
    private readonly IMonitoringPermissionStore _grants;

    /// <summary>Initializes a new instance of the <see cref="MonitoringPermissionEvaluator"/> class.</summary>
    /// <param name="grants">The permission store.</param>
    public MonitoringPermissionEvaluator(IMonitoringPermissionStore grants)
    {
        ArgumentNullException.ThrowIfNull(grants);
        _grants = grants;
    }

    /// <summary>Computes the rights a principal holds, combining any identities it presents.</summary>
    /// <param name="principals">The principal and any roles or groups it belongs to.</param>
    /// <returns>The accumulated rights.</returns>
    public MonitoringPermission Evaluate(params string[] principals)
    {
        ArgumentNullException.ThrowIfNull(principals);
        return principals
            .Where(principal => !string.IsNullOrWhiteSpace(principal))
            .Aggregate(MonitoringPermission.None, (rights, principal) => rights | _grants.Granted(principal));
    }

    /// <summary>Gets a value indicating whether a principal holds a right.</summary>
    /// <param name="permission">The right to test.</param>
    /// <param name="principals">The principal and any roles or groups it belongs to.</param>
    /// <returns><see langword="true"/> when the right is held.</returns>
    public bool Allows(MonitoringPermission permission, params string[] principals) =>
        Evaluate(principals).HasFlag(permission);
}
