using FactoryOS.Gateway.Endpoints;
using FactoryOS.Gateway.Tenancy;
using FactoryOS.Plugins.Oee.Domain;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace FactoryOS.Plugins.Oee.Api;

/// <summary>
/// The OEE read API — the first operations module to expose its read model through the gateway, feeding the OEE
/// dashboard declared in its manifest. It serves the tenant's per-machine OEE snapshots and a factory-wide rollup
/// over <c>/m/oee/*</c> so a wall dashboard, an operator or an AI agent can read effectiveness without touching the
/// production connectors or recomputing anything. It reads only; the model is fed exclusively by
/// <c>ProductionPeriodReported</c> on the bus. The tenant comes from the ambient <see cref="ITenantContext"/> the
/// gateway resolves at the edge, and each route measures against the configured target — behaviour varies by
/// configuration, never by customer.
/// </summary>
internal sealed class OeeApi : IModuleApi
{
    private readonly IOeeStore _store;
    private readonly OeeOptions _options;

    public OeeApi(IOeeStore store, OeeOptions options)
    {
        _store = store;
        _options = options;
    }

    public string ModuleKey => OeePlugin.PluginKey;

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapGet("/snapshots", ([FromServices] ITenantContext context) =>
                Results.Ok(new OeeSnapshotsResponse(context.Tenant, Project(_store.ForTenant(context.Tenant)))))
            .RequireTenant()
            .WithName("GetOeeSnapshots");

        endpoints.MapGet("/summary", ([FromServices] ITenantContext context) =>
                Results.Ok(Summarize(context.Tenant, _store.ForTenant(context.Tenant))))
            .RequireTenant()
            .WithName("GetOeeSummary");
    }

    private List<OeeSnapshotView> Project(IReadOnlyCollection<OeeSnapshot> snapshots) =>
        snapshots
            .OrderBy(s => s.MachineId, StringComparer.Ordinal)
            .ThenBy(s => s.PeriodStart)
            .Select(s => new OeeSnapshotView(
                s.MachineId,
                s.PeriodStart,
                s.PeriodEnd,
                s.Score.Availability,
                s.Score.Performance,
                s.Score.Quality,
                s.Score.Oee,
                s.Score.Oee >= _options.TargetOee))
            .ToList();

    private OeeSummaryResponse Summarize(string tenant, IReadOnlyCollection<OeeSnapshot> snapshots)
    {
        var count = snapshots.Count;
        var averageOee = count == 0
            ? 0m
            : decimal.Round(snapshots.Sum(s => s.Score.Oee) / count, 4, MidpointRounding.AwayFromZero);
        var belowTarget = snapshots.Count(s => s.Score.Oee < _options.TargetOee);
        return new OeeSummaryResponse(tenant, _options.TargetOee, count, averageOee, belowTarget);
    }
}

/// <summary>A tenant's OEE snapshots, projected for reading.</summary>
/// <param name="Tenant">The tenant the snapshots belong to.</param>
/// <param name="Snapshots">The per-machine snapshots, ordered by machine then period.</param>
internal sealed record OeeSnapshotsResponse(string Tenant, IReadOnlyList<OeeSnapshotView> Snapshots);

/// <summary>One OEE snapshot flattened for a dashboard row, judged against the configured target.</summary>
/// <param name="MachineId">The machine.</param>
/// <param name="PeriodStart">The period start.</param>
/// <param name="PeriodEnd">The period end.</param>
/// <param name="Availability">Availability factor in <c>[0, 1]</c>.</param>
/// <param name="Performance">Performance factor in <c>[0, 1]</c>.</param>
/// <param name="Quality">Quality factor in <c>[0, 1]</c>.</param>
/// <param name="Oee">The OEE (Availability × Performance × Quality).</param>
/// <param name="MeetsTarget">Whether the OEE meets the configured target.</param>
internal sealed record OeeSnapshotView(
    string MachineId,
    DateTimeOffset PeriodStart,
    DateTimeOffset PeriodEnd,
    decimal Availability,
    decimal Performance,
    decimal Quality,
    decimal Oee,
    bool MeetsTarget);

/// <summary>A factory-wide OEE rollup for a tenant.</summary>
/// <param name="Tenant">The tenant summarized.</param>
/// <param name="Target">The configured OEE target the snapshots are measured against.</param>
/// <param name="Snapshots">How many machine-period snapshots are counted.</param>
/// <param name="AverageOee">The mean OEE across the snapshots (0 when there are none).</param>
/// <param name="BelowTarget">How many snapshots fall below the target.</param>
internal sealed record OeeSummaryResponse(
    string Tenant,
    decimal Target,
    int Snapshots,
    decimal AverageOee,
    int BelowTarget);
