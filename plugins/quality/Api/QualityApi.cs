using FactoryOS.Contracts.Events;
using FactoryOS.Gateway.Endpoints;
using FactoryOS.Gateway.Tenancy;
using FactoryOS.Plugins.Quality.Domain;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace FactoryOS.Plugins.Quality.Api;

/// <summary>
/// The Quality read API — exposes the tenant's rolling defect-rate read model through the gateway under
/// <c>/m/quality/*</c> so a wall dashboard, a line supervisor or an AI agent can read current per-line defect rates
/// and which lines breach the threshold without touching the inspection sources. It reads only; the windows are fed
/// exclusively by <c>QualityInspectionRecorded</c> on the bus. The tenant comes from the ambient
/// <see cref="ITenantContext"/> the gateway resolves at the edge, and the breach flag is computed by the very same
/// <see cref="DefectRateEvaluator"/> the alerting handler uses — so the dashboard and the alerts always agree.
/// </summary>
internal sealed class QualityApi : IModuleApi
{
    private readonly IDefectRateWindowStore _windows;
    private readonly IQuarantineStore _quarantines;
    private readonly QualityOptions _options;

    public QualityApi(IDefectRateWindowStore windows, IQuarantineStore quarantines, QualityOptions options)
    {
        _windows = windows;
        _quarantines = quarantines;
        _options = options;
    }

    public string ModuleKey => QualityPlugin.PluginKey;

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapGet("/lines", ([FromServices] ITenantContext context, bool? breaching) =>
                Results.Ok(new QualityLinesResponse(context.Tenant, Project(_windows.ForTenant(context.Tenant), breaching ?? false))))
            .RequireTenant()
            .WithName("GetQualityLines");

        endpoints.MapGet("/summary", ([FromServices] ITenantContext context) =>
                Results.Ok(Summarize(context.Tenant, _windows.ForTenant(context.Tenant))))
            .RequireTenant()
            .WithName("GetQualitySummary");

        // The write side: placing a line under quarantine — a manual hold pending inspection. Authorized at the
        // boundary ('quality.quarantine'); on a real transition the module announces QualityLineQuarantined on the
        // bus so Activity and the dashboard react without referencing Quality. Re-quarantining is a no-op, not an error.
        endpoints.MapPost("/lines/{lineId}/quarantine", async (
                string lineId,
                HttpContext http,
                [FromServices] ITenantContext context,
                [FromServices] IEventBus bus,
                QuarantineRequest? body,
                CancellationToken cancellationToken) =>
            {
                var newly = _quarantines.TryQuarantine(context.Tenant, lineId);
                if (newly)
                {
                    await bus.PublishAsync(
                        new QualityLineQuarantined
                        {
                            Tenant = context.Tenant,
                            LineId = lineId,
                            QuarantinedBy = http.User.Identity?.Name,
                            Reason = body?.Reason,
                        },
                        cancellationToken: cancellationToken).ConfigureAwait(false);
                }

                return Results.Ok(new QuarantineResult(context.Tenant, lineId, Quarantined: true, NewlyQuarantined: newly));
            })
            .RequireTenant()
            .RequirePermission("quality.quarantine")
            .WithName("QuarantineQualityLine");
    }

    private bool Breaches(DefectRateSnapshot window) => DefectRateEvaluator.Evaluate(window, _options).IsBreach;

    private List<QualityLineView> Project(IReadOnlyCollection<QualityLineSnapshot> lines, bool breachingOnly)
    {
        IEnumerable<QualityLineView> rows = lines.Select(line => new QualityLineView(
            line.LineId,
            line.ProductId,
            line.Window.InspectedUnits,
            line.Window.DefectiveUnits,
            decimal.Round(line.Window.DefectRate, 4, MidpointRounding.AwayFromZero),
            Breaches(line.Window),
            _quarantines.IsQuarantined(line.Tenant, line.LineId)));

        if (breachingOnly)
        {
            rows = rows.Where(r => r.BreachesThreshold);
        }

        return rows
            .OrderBy(r => r.LineId, StringComparer.Ordinal)
            .ThenBy(r => r.ProductId, StringComparer.Ordinal)
            .ToList();
    }

    private QualitySummaryResponse Summarize(string tenant, IReadOnlyCollection<QualityLineSnapshot> lines)
    {
        var breaching = lines.Count(line => Breaches(line.Window));
        return new QualitySummaryResponse(tenant, _options.DefectRateThreshold, lines.Count, breaching);
    }
}

/// <summary>A tenant's per-line defect rates, projected for reading.</summary>
/// <param name="Tenant">The tenant the lines belong to.</param>
/// <param name="Lines">The per-line rates, ordered by line then product.</param>
internal sealed record QualityLinesResponse(string Tenant, IReadOnlyList<QualityLineView> Lines);

/// <summary>One line-product aggregate's current defect rate, flattened for a dashboard row.</summary>
/// <param name="LineId">The production line or workstation.</param>
/// <param name="ProductId">The product.</param>
/// <param name="InspectedUnits">Units inspected across the current window.</param>
/// <param name="DefectiveUnits">Defective units across the current window.</param>
/// <param name="DefectRate">The window defect rate, a fraction in <c>[0, 1]</c>.</param>
/// <param name="BreachesThreshold">Whether the rate breaches the threshold with enough evidence.</param>
/// <param name="Quarantined">Whether the line is currently held under quarantine.</param>
internal sealed record QualityLineView(
    string LineId,
    string ProductId,
    int InspectedUnits,
    int DefectiveUnits,
    decimal DefectRate,
    bool BreachesThreshold,
    bool Quarantined);

/// <summary>The optional body of a quarantine request.</summary>
/// <param name="Reason">An optional reason recorded with the quarantine.</param>
internal sealed record QuarantineRequest(string? Reason);

/// <summary>The result of a quarantine request.</summary>
/// <param name="Tenant">The tenant the line belongs to.</param>
/// <param name="LineId">The line addressed.</param>
/// <param name="Quarantined">Whether the line is now under quarantine (always true on success).</param>
/// <param name="NewlyQuarantined">Whether this request is the one that transitioned it (false when already held).</param>
internal sealed record QuarantineResult(string Tenant, string LineId, bool Quarantined, bool NewlyQuarantined);

/// <summary>A tenant's quality rollup.</summary>
/// <param name="Tenant">The tenant summarized.</param>
/// <param name="Threshold">The configured defect-rate threshold breaches are measured against.</param>
/// <param name="Lines">How many line-product aggregates are tracked.</param>
/// <param name="Breaching">How many are currently breaching the threshold.</param>
internal sealed record QualitySummaryResponse(string Tenant, decimal Threshold, int Lines, int Breaching);
