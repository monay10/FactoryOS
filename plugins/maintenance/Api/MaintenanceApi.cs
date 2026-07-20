using FactoryOS.Contracts.Events;
using FactoryOS.Contracts.StandardModel;
using FactoryOS.Gateway.Endpoints;
using FactoryOS.Gateway.Tenancy;
using FactoryOS.Plugins.Maintenance.Domain;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace FactoryOS.Plugins.Maintenance.Api;

/// <summary>
/// The Maintenance read API — exposes the tenant's work-order backlog through the gateway under
/// <c>/m/maintenance/*</c> so a wall dashboard, a technician's screen or an AI agent can read the maintenance
/// to-do list without touching the modules that raise the orders. It reads only; the backlog is fed exclusively
/// by <c>EnergySpikeDetected</c> and <c>RuleTriggered</c> on the bus. The tenant comes from the ambient
/// <see cref="ITenantContext"/> the gateway resolves at the edge, and every route is guarded with
/// <c>RequireTenant()</c>.
/// </summary>
internal sealed class MaintenanceApi : IModuleApi
{
    private readonly IWorkOrderStore _store;

    public MaintenanceApi(IWorkOrderStore store)
    {
        _store = store;
    }

    public string ModuleKey => MaintenancePlugin.PluginKey;

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapGet("/workorders", ([FromServices] ITenantContext context, string? status) =>
                Results.Ok(new WorkOrdersResponse(context.Tenant, Project(_store.ForTenant(context.Tenant), status))))
            .RequireTenant()
            .WithName("GetWorkOrders");

        endpoints.MapGet("/summary", ([FromServices] ITenantContext context) =>
                Results.Ok(Summarize(context.Tenant, _store.ForTenant(context.Tenant))))
            .RequireTenant()
            .WithName("GetWorkOrderSummary");

        // The write side: closing a work order. Authorized at the boundary — a caller must hold 'maintenance.close',
        // not merely have the screen visible. On a real transition the module announces WorkOrderClosed on the bus so
        // other modules react without referencing Maintenance; closing an already-closed order is a no-op, not an error.
        endpoints.MapPost("/workorders/{number}/close", async (
                string number,
                HttpContext http,
                [FromServices] ITenantContext context,
                [FromServices] IEventBus bus,
                CancellationToken cancellationToken) =>
            {
                var outcome = _store.Close(context.Tenant, number);
                if (outcome.Result == WorkOrderCloseResult.NotFound)
                {
                    return Results.NotFound(new { error = $"Work order '{number}' was not found." });
                }

                var workOrder = outcome.WorkOrder!;
                if (outcome.Result == WorkOrderCloseResult.Closed)
                {
                    await bus.PublishAsync(
                        new WorkOrderClosed { WorkOrder = workOrder, ClosedBy = http.User.Identity?.Name },
                        cancellationToken: cancellationToken).ConfigureAwait(false);
                }

                return Results.Ok(new WorkOrderView(
                    workOrder.Number, workOrder.Title, workOrder.Status, workOrder.AssetCode, workOrder.DueAt));
            })
            .RequireTenant()
            .RequirePermission("maintenance.close")
            .WithName("CloseWorkOrder");
    }

    private static List<WorkOrderView> Project(IReadOnlyCollection<WorkOrder> workOrders, string? status)
    {
        IEnumerable<WorkOrder> selected = workOrders;
        if (!string.IsNullOrWhiteSpace(status))
        {
            selected = selected.Where(w => string.Equals(w.Status, status, StringComparison.OrdinalIgnoreCase));
        }

        return selected
            .OrderBy(w => w.DueAt ?? DateTimeOffset.MaxValue)
            .ThenBy(w => w.Number, StringComparer.Ordinal)
            .Select(w => new WorkOrderView(w.Number, w.Title, w.Status, w.AssetCode, w.DueAt))
            .ToList();
    }

    private static WorkOrderSummaryResponse Summarize(string tenant, IReadOnlyCollection<WorkOrder> workOrders)
    {
        var byStatus = workOrders
            .GroupBy(w => w.Status, StringComparer.OrdinalIgnoreCase)
            .Select(g => new StatusCount(g.Key, g.Count()))
            .OrderBy(s => s.Status, StringComparer.Ordinal)
            .ToList();
        return new WorkOrderSummaryResponse(tenant, workOrders.Count, byStatus);
    }
}

/// <summary>A tenant's work-order backlog, projected for reading.</summary>
/// <param name="Tenant">The tenant the work orders belong to.</param>
/// <param name="WorkOrders">The work orders, soonest-due first then by number.</param>
internal sealed record WorkOrdersResponse(string Tenant, IReadOnlyList<WorkOrderView> WorkOrders);

/// <summary>One work order flattened for a dashboard row.</summary>
/// <param name="Number">The work-order number (natural key within the tenant).</param>
/// <param name="Title">The work-order title or summary.</param>
/// <param name="Status">The current status.</param>
/// <param name="AssetCode">The targeted asset code, if any.</param>
/// <param name="DueAt">When the work order is due, if scheduled.</param>
internal sealed record WorkOrderView(
    string Number,
    string Title,
    string Status,
    string? AssetCode,
    DateTimeOffset? DueAt);

/// <summary>A tenant's work-order rollup.</summary>
/// <param name="Tenant">The tenant summarized.</param>
/// <param name="Total">How many work orders exist.</param>
/// <param name="ByStatus">The per-status counts, ordered by status.</param>
internal sealed record WorkOrderSummaryResponse(string Tenant, int Total, IReadOnlyList<StatusCount> ByStatus);

/// <summary>How many work orders share a status.</summary>
/// <param name="Status">The status value.</param>
/// <param name="Count">How many work orders have it.</param>
internal sealed record StatusCount(string Status, int Count);
