using FactoryOS.Api.Platform;
using FactoryOS.Gateway.Tenancy;
using FactoryOS.Plugins.Runtime.Domain;
using FactoryOS.Plugins.Runtime.Execution;
using Microsoft.AspNetCore.Http;
using AuditEngine = FactoryOS.Plugins.Workflow.Audit.Execution.AuditEngine;
using MonitoringEngine = FactoryOS.Plugins.Workflow.Monitoring.Execution.MonitoringEngine;

namespace Microsoft.AspNetCore.Builder;

/// <summary>
/// The read-only <c>/platform/*</c> observability surface: it lets an operator see, over HTTP, what the composed
/// platform is actually doing — which plugins a tenant runs, how healthy they are, what extends each point, the
/// tenant's audit trail and the monitoring metrics. Every route is tenant-scoped (the request's resolved tenant),
/// so no caller can read another factory's state. Mutating a plugin's lifecycle is a separate, later surface.
/// </summary>
public static class PlatformObservabilityEndpoints
{
    /// <summary>Maps the read-only <c>/platform/*</c> observability endpoints onto the web application.</summary>
    /// <param name="app">The web application.</param>
    /// <returns>The same <see cref="WebApplication"/> instance, to allow chaining.</returns>
    public static WebApplication MapPlatformObservability(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        // The plugins a tenant has installed.
        app.MapGet("/platform/plugins", (ITenantContext tenants, IPluginRuntime runtime) =>
            Resolve(tenants, tenant => Results.Ok(PlatformObservability.Plugins(runtime, tenant))));

        // One plugin's health for the tenant.
        app.MapGet("/platform/plugins/{key}/health", (string key, ITenantContext tenants, IPluginRuntime runtime) =>
            Resolve(tenants, tenant => Results.Ok(PlatformObservability.Health(runtime, tenant, key))));

        // What currently extends a published extension point for the tenant.
        app.MapGet("/platform/extensions/{point}", (string point, ITenantContext tenants, IPluginRuntime runtime) =>
            Resolve(tenants, tenant =>
            {
                if (!PluginExtensionPoints.TryParse(point, out var resolved))
                {
                    return Results.Problem(
                        statusCode: StatusCodes.Status404NotFound,
                        title: "Unknown extension point",
                        detail: $"'{point}' is not a published extension point.");
                }

                return Results.Ok(PlatformObservability.Extensions(runtime, tenant, resolved.Kind));
            }));

        // The tenant's audit trail (newest first) and the chain verification verdict.
        app.MapGet("/platform/audit", (ITenantContext tenants, AuditEngine audit) =>
            Resolve(tenants, tenant => Results.Ok(PlatformObservability.Audit(audit, tenant))));

        // Search the tenant's audit trail (newest first). Every filter is optional and combines with AND; an
        // unrecognised category/action/severity/result/timestamp is a 400 rather than being silently ignored.
        app.MapGet("/platform/audit/search", (
            string? category, string? action, string? severity, string? result, string? actor,
            string? from, string? to, string? contains, int? limit, ITenantContext tenants, AuditEngine audit) =>
            Resolve(tenants, tenant =>
            {
                var parse = PlatformObservability.ParseAuditQuery(
                    tenant, category, action, severity, result, actor, from, to, contains, limit);

                return parse.Ok
                    ? Results.Ok(PlatformObservability.SearchAudit(audit, parse.Query!))
                    : Results.Problem(
                        statusCode: StatusCodes.Status400BadRequest, title: "Invalid audit filter", detail: parse.Error);
            }));

        // The monitoring engine's counters and registered metric definitions.
        app.MapGet("/platform/metrics", (ITenantContext tenants, MonitoringEngine monitoring) =>
            Resolve(tenants, _ => Results.Ok(PlatformObservability.Metrics(monitoring))));

        return app;
    }

    // Multi-tenancy is a construction rule: every /platform read runs on behalf of exactly one factory. A request
    // that resolved no tenant is refused rather than defaulted, so there is no path that reads across tenants.
    private static IResult Resolve(ITenantContext tenants, Func<string, IResult> withTenant)
    {
        return tenants.TryGetTenant(out var tenant)
            ? withTenant(tenant)
            : Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "No tenant in scope",
                detail: "This endpoint is tenant-scoped; the request did not resolve a tenant.");
    }
}
