using FactoryOS.Gateway.Endpoints;
using FactoryOS.Gateway.Tenancy;
using FactoryOS.Plugins.Dashboard.Domain;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace FactoryOS.Plugins.Dashboard.Api;

/// <summary>
/// The Dashboard read API — the Experience layer's single aggregated feed. It exposes the tenant's live operations
/// board through the gateway under <c>/m/dashboard/*</c> so a wall screen, a PWA or an AI agent can read the whole
/// factory at a glance — the latest OEE per machine and the recent alert feed — in one call, without touching any
/// module. It reads only; the board is kept current purely by consuming <c>OeeCalculated</c> and the alert events
/// on the bus. The tenant comes from the ambient <see cref="ITenantContext"/> the gateway resolves at the edge.
/// </summary>
internal sealed class DashboardApi : IModuleApi
{
    private readonly IOperationsBoard _board;

    public DashboardApi(IOperationsBoard board)
    {
        _board = board;
    }

    public string ModuleKey => DashboardPlugin.PluginKey;

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapGet("/board", ([FromServices] ITenantContext context, string? level, string? kind) =>
                Results.Ok(Project(_board.Snapshot(context.Tenant), level, kind)))
            .RequireTenant()
            .WithName("GetDashboardBoard");

        endpoints.MapGet("/summary", ([FromServices] ITenantContext context) =>
                Results.Ok(Summarize(_board.Snapshot(context.Tenant))))
            .RequireTenant()
            .WithName("GetDashboardSummary");
    }

    private static DashboardBoardResponse Project(BoardSnapshot snapshot, string? level, string? kind)
    {
        var machines = snapshot.Machines
            .Select(static m => new DashboardMachineView(
                m.MachineId,
                decimal.Round(m.Oee, 4, MidpointRounding.AwayFromZero),
                m.MeetsTarget,
                m.AsOf))
            .ToList();

        IEnumerable<AlertTile> feed = snapshot.RecentAlerts;
        if (!string.IsNullOrWhiteSpace(level))
        {
            feed = feed.Where(a => string.Equals(a.Level, level, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(kind))
        {
            feed = feed.Where(a => string.Equals(a.Kind, kind, StringComparison.OrdinalIgnoreCase));
        }

        var alerts = feed
            .Select(static a => new DashboardAlertView(a.Kind, a.Level, a.Subject, a.OccurredAt))
            .ToList();

        // CriticalAlertCount stays a whole-board headline even when the feed is filtered by level or kind.
        return new DashboardBoardResponse(snapshot.Tenant, machines, alerts, snapshot.CriticalAlertCount);
    }

    private static DashboardSummaryResponse Summarize(BoardSnapshot snapshot)
    {
        var belowTarget = snapshot.Machines.Count(static m => !m.MeetsTarget);

        var byKind = snapshot.RecentAlerts
            .GroupBy(static a => a.Kind, StringComparer.Ordinal)
            .Select(static g => new DashboardAlertKindTally(g.Key, g.Count()))
            .OrderByDescending(static t => t.Count)
            .ThenBy(static t => t.Kind, StringComparer.Ordinal)
            .ToList();

        return new DashboardSummaryResponse(
            snapshot.Tenant,
            snapshot.Machines.Count,
            belowTarget,
            snapshot.RecentAlerts.Count,
            snapshot.CriticalAlertCount,
            byKind);
    }
}

/// <summary>A tenant's live operations board, projected for reading.</summary>
/// <param name="Tenant">The tenant the board belongs to.</param>
/// <param name="Machines">Latest OEE per machine, ordered by machine id.</param>
/// <param name="Alerts">The recent alert feed, newest first, optionally filtered by level and/or kind.</param>
/// <param name="CriticalAlertCount">How many alerts across the whole feed are critical (unaffected by the level filter).</param>
internal sealed record DashboardBoardResponse(
    string Tenant,
    IReadOnlyList<DashboardMachineView> Machines,
    IReadOnlyList<DashboardAlertView> Alerts,
    int CriticalAlertCount);

/// <summary>One machine's latest OEE, flattened for a dashboard tile.</summary>
/// <param name="MachineId">The machine.</param>
/// <param name="Oee">Overall Equipment Effectiveness, a fraction in <c>[0, 1]</c>.</param>
/// <param name="MeetsTarget">Whether the latest OEE met the machine's configured target.</param>
/// <param name="AsOf">The end of the period this OEE was computed for.</param>
internal sealed record DashboardMachineView(string MachineId, decimal Oee, bool MeetsTarget, DateTimeOffset AsOf);

/// <summary>One entry in the live alert feed, flattened for a dashboard row.</summary>
/// <param name="Kind">The originating event type (for example <c>SafetyStandDownTriggered</c>).</param>
/// <param name="Level">The normalized urgency, one of <see cref="AlertLevels"/>.</param>
/// <param name="Subject">A human-readable description of the alert.</param>
/// <param name="OccurredAt">When the alert-triggering event occurred.</param>
internal sealed record DashboardAlertView(string Kind, string Level, string Subject, DateTimeOffset OccurredAt);

/// <summary>A tenant's board headline for an at-a-glance tile strip.</summary>
/// <param name="Tenant">The tenant summarized.</param>
/// <param name="Machines">How many machines are on the board.</param>
/// <param name="MachinesBelowTarget">How many are currently below their OEE target.</param>
/// <param name="RecentAlerts">How many alerts are in the live feed.</param>
/// <param name="CriticalAlerts">How many of those alerts are critical.</param>
/// <param name="AlertsByKind">A per-kind tally of the live feed, ordered by count descending (ties by kind name).</param>
internal sealed record DashboardSummaryResponse(
    string Tenant,
    int Machines,
    int MachinesBelowTarget,
    int RecentAlerts,
    int CriticalAlerts,
    IReadOnlyList<DashboardAlertKindTally> AlertsByKind);

/// <summary>One row of the summary's per-kind alert breakdown.</summary>
/// <param name="Kind">The originating event type (for example <c>EnergySpikeDetected</c>).</param>
/// <param name="Count">How many alerts of this kind are in the live feed.</param>
internal sealed record DashboardAlertKindTally(string Kind, int Count);
