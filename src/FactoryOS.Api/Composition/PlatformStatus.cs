using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FactoryOS.Plugins.Runtime.Execution;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using ApprovalEngine = FactoryOS.Plugins.Workflow.Approvals.Execution.ApprovalEngine;
using AuditEngine = FactoryOS.Plugins.Workflow.Audit.Execution.AuditEngine;
using ConnectorEngine = FactoryOS.Connectors.Runtime.Execution.ConnectorEngine;
using FormEngine = FactoryOS.Plugins.Forms.Engine.Execution.FormEngine;
using HumanTaskEngine = FactoryOS.Plugins.Workflow.Tasks.Execution.HumanTaskEngine;
using MonitoringEngine = FactoryOS.Plugins.Workflow.Monitoring.Execution.MonitoringEngine;
using NotificationEngine = FactoryOS.Plugins.Workflow.Notifications.Execution.NotificationEngine;
using SecurityEngine = FactoryOS.Plugins.Workflow.Security.Execution.SecurityEngine;
using SlaEngine = FactoryOS.Plugins.Workflow.SLA.Execution.SlaEngine;
using WorkflowStateEngine = FactoryOS.Plugins.Workflow.Engine.Execution.WorkflowEngine;

namespace FactoryOS.Api.Composition;

/// <summary>One platform component and whether it resolved from the running host's container.</summary>
/// <param name="Name">The component's display name.</param>
/// <param name="Service">The service type the composition root registered it as.</param>
/// <param name="Up">Whether the running container could resolve it.</param>
public sealed record PlatformComponent(string Name, string Service, bool Up);

/// <summary>
/// Reports which platform engines and runtimes are actually live in the running host. This is the proof that the
/// engines are composed into the deployed process, not merely present as library code: every component is
/// resolved from the same container the request pipeline uses.
/// </summary>
public static class PlatformStatus
{
    private static readonly (string Name, Type Service)[] Components =
    [
        ("Security engine", typeof(SecurityEngine)),
        ("Audit engine", typeof(AuditEngine)),
        ("Monitoring engine", typeof(MonitoringEngine)),
        ("Approval engine", typeof(ApprovalEngine)),
        ("SLA engine", typeof(SlaEngine)),
        ("Notification engine", typeof(NotificationEngine)),
        ("Human task engine", typeof(HumanTaskEngine)),
        ("Forms engine", typeof(FormEngine)),
        ("Workflow engine", typeof(WorkflowStateEngine)),
        ("Connector runtime", typeof(ConnectorEngine)),
        ("Plugin runtime", typeof(IPluginRuntime)),
    ];

    /// <summary>Resolves every platform component from the given provider and reports whether each is live.</summary>
    /// <param name="provider">The service provider backing the running host.</param>
    /// <returns>One entry per platform engine and runtime.</returns>
    public static IReadOnlyList<PlatformComponent> Describe(IServiceProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);

        return [.. Components.Select(component => new PlatformComponent(
            component.Name,
            component.Service.Name,
            provider.GetService(component.Service) is not null))];
    }
}

/// <summary>
/// The health check that verifies every platform engine and runtime resolves from the running container. It is
/// Healthy only when all are live; a missing component is reported by name.
/// </summary>
public sealed class PlatformStatusHealthCheck : IHealthCheck
{
    private readonly IServiceProvider _provider;

    /// <summary>Creates the health check over the host's service provider.</summary>
    /// <param name="provider">The service provider backing the running host.</param>
    public PlatformStatusHealthCheck(IServiceProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        _provider = provider;
    }

    /// <inheritdoc />
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var components = PlatformStatus.Describe(_provider);
        var data = components.ToDictionary(
            component => component.Name, component => (object)(component.Up ? "up" : "down"));

        var down = components.Where(component => !component.Up).Select(component => component.Name).ToArray();
        var result = down.Length == 0
            ? HealthCheckResult.Healthy(
                $"All {components.Count} platform engines and runtimes are live.", data)
            : HealthCheckResult.Unhealthy(
                $"Platform components not live: {string.Join(", ", down)}.", data: data);

        return Task.FromResult(result);
    }
}

/// <summary>Endpoint wiring for the platform status surface.</summary>
public static class PlatformStatusEndpoint
{
    /// <summary>
    /// Maps <c>GET /platform/status</c>, a readable JSON listing of every platform engine and runtime and
    /// whether it is live in the running host.
    /// </summary>
    /// <param name="app">The web application.</param>
    /// <returns>The same <see cref="WebApplication"/> instance, to allow chaining.</returns>
    public static WebApplication MapPlatformStatus(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet("/platform/status", (IServiceProvider provider) =>
        {
            var components = PlatformStatus.Describe(provider);
            return Results.Json(new
            {
                status = components.All(component => component.Up) ? "healthy" : "degraded",
                total = components.Count,
                components,
            });
        });

        return app;
    }
}
