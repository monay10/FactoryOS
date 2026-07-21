using System.Collections.Concurrent;
using FactoryOS.Plugins.Workflow.Audit.Domain;
using FactoryOS.Plugins.Workflow.Audit.Events;

namespace FactoryOS.Plugins.Workflow.Audit.Execution;

/// <summary>
/// Publishes audit lifecycle events to every registered sink. The fan-out is deliberate: audit sits at the
/// bottom of the stack and several things legitimately watch it at once — a recorder, a SIEM forwarder, an
/// alerting rule on tamper detection — and none of them should be able to displace another.
/// </summary>
public sealed class AuditDispatcher
{
    private readonly IEnumerable<IAuditEventSink> _sinks;

    /// <summary>Initializes a new instance of the <see cref="AuditDispatcher"/> class.</summary>
    /// <param name="sinks">The sinks to fan out to.</param>
    public AuditDispatcher(IEnumerable<IAuditEventSink> sinks)
    {
        ArgumentNullException.ThrowIfNull(sinks);
        _sinks = sinks;
    }

    /// <summary>Publishes an audit event to every sink.</summary>
    /// <param name="auditEvent">The event.</param>
    public void Publish(AuditEvent auditEvent)
    {
        ArgumentNullException.ThrowIfNull(auditEvent);
        foreach (var sink in _sinks)
        {
            sink.Publish(auditEvent);
        }
    }
}

/// <summary>Holds the audit rights granted to principals.</summary>
public interface IAuditPermissionStore
{
    /// <summary>Grants rights to a principal (grants accumulate).</summary>
    /// <param name="principal">The principal (a user id, <c>role:x</c> or <c>group:x</c>).</param>
    /// <param name="permission">The rights to grant.</param>
    void Grant(string principal, AuditPermission permission);

    /// <summary>Gets the rights granted to a principal.</summary>
    /// <param name="principal">The principal.</param>
    /// <returns>The accumulated rights.</returns>
    AuditPermission Granted(string principal);
}

/// <summary>An in-memory <see cref="IAuditPermissionStore"/>.</summary>
public sealed class InMemoryAuditPermissionStore : IAuditPermissionStore
{
    private readonly ConcurrentDictionary<string, AuditPermission> _grants = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public void Grant(string principal, AuditPermission permission)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(principal);
        _grants.AddOrUpdate(principal, permission, (_, existing) => existing | permission);
    }

    /// <inheritdoc />
    public AuditPermission Granted(string principal)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(principal);
        return _grants.TryGetValue(principal, out var permission) ? permission : AuditPermission.None;
    }
}

/// <summary>
/// Decides what a principal may do with the audit trail. Rights are granted explicitly and accumulate across a
/// principal's identities, so a user can be granted read access directly while their role carries the right to
/// export. Nothing is implicit: reading an audit trail is itself a privileged act.
/// </summary>
public sealed class AuditPermissionEvaluator
{
    private readonly IAuditPermissionStore _grants;

    /// <summary>Initializes a new instance of the <see cref="AuditPermissionEvaluator"/> class.</summary>
    /// <param name="grants">The permission store.</param>
    public AuditPermissionEvaluator(IAuditPermissionStore grants)
    {
        ArgumentNullException.ThrowIfNull(grants);
        _grants = grants;
    }

    /// <summary>Computes the rights a principal holds, combining any identities it presents.</summary>
    /// <param name="principals">The principal and any roles or groups it belongs to.</param>
    /// <returns>The accumulated rights.</returns>
    public AuditPermission Evaluate(params string[] principals)
    {
        ArgumentNullException.ThrowIfNull(principals);
        return principals
            .Where(principal => !string.IsNullOrWhiteSpace(principal))
            .Aggregate(AuditPermission.None, (rights, principal) => rights | _grants.Granted(principal));
    }

    /// <summary>Gets a value indicating whether a principal holds a right.</summary>
    /// <param name="permission">The right to test.</param>
    /// <param name="principals">The principal and any roles or groups it belongs to.</param>
    /// <returns><see langword="true"/> when the right is held.</returns>
    public bool Allows(AuditPermission permission, params string[] principals) =>
        Evaluate(principals).HasFlag(permission);
}
