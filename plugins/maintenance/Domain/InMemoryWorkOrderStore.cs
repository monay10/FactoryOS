using System.Collections.Concurrent;
using FactoryOS.Contracts.StandardModel;

namespace FactoryOS.Plugins.Maintenance.Domain;

/// <summary>
/// The default in-memory <see cref="IWorkOrderStore"/>: a per-tenant map of work orders keyed by number. Each
/// tenant has its own bucket, so no tenant can read or overwrite another's. Replaceable by an EF Core-backed
/// store behind the interface.
/// </summary>
public sealed class InMemoryWorkOrderStore : IWorkOrderStore
{
    /// <summary>The canonical closed status a work order carries once completed.</summary>
    public const string ClosedStatus = "Closed";

    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, WorkOrder>> _byTenant =
        new(StringComparer.Ordinal);

    /// <inheritdoc />
    public bool TryAdd(WorkOrder workOrder)
    {
        ArgumentNullException.ThrowIfNull(workOrder);
        var bucket = _byTenant.GetOrAdd(workOrder.Tenant, static _ => new ConcurrentDictionary<string, WorkOrder>(StringComparer.Ordinal));
        return bucket.TryAdd(workOrder.Number, workOrder);
    }

    /// <inheritdoc />
    public IReadOnlyCollection<WorkOrder> ForTenant(string tenant)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);
        return _byTenant.TryGetValue(tenant, out var bucket) ? bucket.Values.ToList() : [];
    }

    /// <inheritdoc />
    public WorkOrderCloseOutcome Close(string tenant, string number)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);
        ArgumentException.ThrowIfNullOrWhiteSpace(number);

        if (!_byTenant.TryGetValue(tenant, out var bucket) || !bucket.TryGetValue(number, out var existing))
        {
            return new WorkOrderCloseOutcome(WorkOrderCloseResult.NotFound, null);
        }

        if (string.Equals(existing.Status, ClosedStatus, StringComparison.OrdinalIgnoreCase))
        {
            return new WorkOrderCloseOutcome(WorkOrderCloseResult.AlreadyClosed, existing);
        }

        var closed = existing with { Status = ClosedStatus };

        // Only report a transition when this call is the one that closed it, so concurrent closers do not each publish.
        return bucket.TryUpdate(number, closed, existing)
            ? new WorkOrderCloseOutcome(WorkOrderCloseResult.Closed, closed)
            : new WorkOrderCloseOutcome(WorkOrderCloseResult.AlreadyClosed, bucket.GetValueOrDefault(number) ?? closed);
    }
}
