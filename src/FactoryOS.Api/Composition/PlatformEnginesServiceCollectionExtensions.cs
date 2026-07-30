using FactoryOS.Api.Composition;
using FactoryOS.Domain.Abstractions;
using FactoryOS.Plugins.Runtime.Integration;
using FactoryOS.Plugins.Runtime.Security;
using FactoryOS.Plugins.Workflow.Audit.Execution;
using FactoryOS.Plugins.Workflow.Monitoring.Execution;
using FactoryOS.Plugins.Workflow.Security.Execution;
using Microsoft.Extensions.Configuration;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// The composition root's <b>platform engine</b> wiring. It brings the platform engines (Security, Audit,
/// Monitoring, Approval, SLA, Notification, Human Task, Forms, Workflow) and the two platform runtimes
/// (Connector Runtime, Plugin Runtime) into the running host's service container, and binds the plugin
/// runtime's ports to the engines so a plugin's authorization, audit and metrics flow through the platform.
/// <para>
/// This is composition-root wiring, not customer code — nothing here branches on a tenant. Every engine
/// registration is self-contained (in-memory stores by default), so the host starts without a database. The
/// engines physically live in the workflow plugin assembly today; their proper home is a dedicated Platform
/// assembly, a move tracked separately. See <c>Composition/README.md</c>.
/// </para>
/// </summary>
public static class PlatformEnginesServiceCollectionExtensions
{
    /// <summary>
    /// Registers every platform engine, the two platform runtimes, and the engine-backed plugin runtime ports.
    /// </summary>
    /// <param name="services">The service collection to add registrations to.</param>
    /// <param name="configuration">The application configuration, bound into the plugin runtime options.</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance, to allow chaining.</returns>
    public static IServiceCollection AddPlatformEngines(
        this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        // The platform engines. Each registration is idempotent (TryAdd) and self-contained.
        services.AddSecurityEngine();
        services.AddAuditEngine();
        services.AddMonitoringEngine();
        services.AddApprovalEngine();
        services.AddSlaEngine();
        services.AddNotificationEngine();
        services.AddHumanTaskEngine();
        services.AddFormsEngine();
        services.AddWorkflowEngine();

        // Opt-in cross-engine integrations, exactly as a deployment would choose them: security decisions land
        // in the audit trail and are measured; SLA breaches raise notifications.
        services.AddSecurityAuditIntegration();
        services.AddSecurityMonitoringIntegration();
        services.AddSlaNotificationIntegration();

        // Bind the plugin runtime's ports to the engines. These are registered BEFORE AddPluginRuntime so its
        // TryAdd defaults (the in-memory authorizer, audit sink and metric sink) defer to these — the runtime
        // then authorizes through the security engine and writes its trail and metrics to the platform.
        services.AddSingleton<IPluginAuthorizer>(provider => new SecurityEnginePluginAuthorizer(
            provider.GetRequiredService<SecurityEngine>(),
            provider.GetRequiredService<IDateTimeProvider>()));
        services.AddSingleton<IPluginAuditSink>(provider =>
            new AuditEnginePluginSink(provider.GetRequiredService<AuditEngine>()));
        services.AddSingleton<IPluginMetricSink>(provider =>
            new MonitoringEnginePluginSink(provider.GetRequiredService<MonitoringEngine>()));

        // The two platform runtimes. The plugin runtime binds its options from Plugins:Runtime.
        services.AddConnectorRuntime();
        services.AddPluginRuntime(configuration);

        // A readiness check that proves every engine and runtime actually resolves from the running container.
        services.AddHealthChecks().AddCheck<PlatformStatusHealthCheck>("platform", tags: ["ready"]);

        return services;
    }
}
