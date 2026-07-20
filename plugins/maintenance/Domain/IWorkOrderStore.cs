using FactoryOS.Contracts.StandardModel;

namespace FactoryOS.Plugins.Maintenance.Domain;

/// <summary>
/// Stores work orders per tenant. Adding is idempotent by work-order number, which both persists the order and
/// guards against creating it twice for the same trigger. Tenant-scoped by construction.
/// </summary>
public interface IWorkOrderStore
{
    /// <summary>Adds a work order if its number is not already present for the tenant.</summary>
    /// <param name="workOrder">The work order to add.</param>
    /// <returns><see langword="true"/> if added; <see langword="false"/> if the number already existed.</returns>
    bool TryAdd(WorkOrder workOrder);

    /// <summary>Returns all work orders for a tenant.</summary>
    /// <param name="tenant">The tenant to list within.</param>
    /// <returns>The tenant's work orders.</returns>
    IReadOnlyCollection<WorkOrder> ForTenant(string tenant);

    /// <summary>
    /// Transitions a tenant's work order to <c>Closed</c>. Idempotent: closing an already-closed order reports
    /// <see cref="WorkOrderCloseResult.AlreadyClosed"/> without changing it, and an unknown number reports
    /// <see cref="WorkOrderCloseResult.NotFound"/>, so a caller can publish the close event only on a real transition.
    /// </summary>
    /// <param name="tenant">The tenant the work order belongs to.</param>
    /// <param name="number">The work-order number to close.</param>
    /// <returns>The outcome and, when the order exists, its (now or already) closed state.</returns>
    WorkOrderCloseOutcome Close(string tenant, string number);
}

/// <summary>The result of attempting to close a work order.</summary>
public enum WorkOrderCloseResult
{
    /// <summary>No work order with the given number exists for the tenant.</summary>
    NotFound,

    /// <summary>The work order was already closed; nothing changed.</summary>
    AlreadyClosed,

    /// <summary>The work order transitioned to closed as a result of this call.</summary>
    Closed,
}

/// <summary>The outcome of a close attempt, with the work order when it exists.</summary>
/// <param name="Result">Whether the order was closed now, already closed, or not found.</param>
/// <param name="WorkOrder">The work order in its closed state, or <see langword="null"/> when not found.</param>
public readonly record struct WorkOrderCloseOutcome(WorkOrderCloseResult Result, WorkOrder? WorkOrder);
