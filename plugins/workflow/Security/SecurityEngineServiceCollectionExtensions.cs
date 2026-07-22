using FactoryOS.Domain.Abstractions;
using FactoryOS.Domain.Time;
using FactoryOS.Plugins.Workflow.Security.Configuration;
using FactoryOS.Plugins.Workflow.Security.Diagnostics;
using FactoryOS.Plugins.Workflow.Security.Events;
using FactoryOS.Plugins.Workflow.Security.Execution;
using FactoryOS.Plugins.Workflow.Security.Integration;
using FactoryOS.Plugins.Workflow.Security.Localization;
using FactoryOS.Plugins.Workflow.Security.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registration entry point for the FactoryOS <b>Security engine</b> — the platform's authorization decision
/// layer. It registers the permission, role, claim and policy evaluators, the authorization engine, session
/// management and token validation, and the stores behind them.
/// <para>
/// The core registration depends on <b>no other engine</b>. Security is a platform service that engines speak
/// to through a shared vocabulary — a permission string, a principal, a claim — and nothing in the workflow,
/// forms, human task, approval, notification, SLA, audit or monitoring engines references it. Producing an
/// audit trail and metrics from security events is opt-in, through the two bridges below, exactly as the SLA
/// engine's notification integration is.
/// </para>
/// </summary>
public static class SecurityEngineServiceCollectionExtensions
{
    /// <summary>Registers the security engine.</summary>
    /// <param name="services">The service collection to add registrations to.</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance, to allow chaining.</returns>
    public static IServiceCollection AddSecurityEngine(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton(new SecurityEngineOptions());
        services.TryAddSingleton<IDateTimeProvider, SystemDateTimeProvider>();

        services.TryAddSingleton<ISecurityRepository, InMemorySecurityRepository>();
        services.TryAddSingleton<ISecurityStore, InMemorySecurityStore>();
        services.TryAddSingleton<ISessionRepository, InMemorySessionRepository>();
        services.TryAddSingleton<ITokenRepository, InMemoryTokenRepository>();
        services.TryAddSingleton<ISecurityLocalizer, InMemorySecurityLocalizer>();

        // The security event seam fans out, so the audit bridge, the monitoring bridge and anything a later
        // commit adds can all observe the same stream without displacing each other.
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<ISecurityEventSink, InMemorySecurityEventSink>());

        services.TryAddSingleton<SecurityMetrics>();
        services.TryAddSingleton<RoleResolver>();
        services.TryAddSingleton<ClaimResolver>();
        services.TryAddSingleton<PermissionEvaluator>();
        services.TryAddSingleton<PolicyEvaluator>();
        services.TryAddSingleton<AuthorizationEngine>();
        services.TryAddSingleton<SessionManager>();
        services.TryAddSingleton<TokenValidator>();
        services.TryAddSingleton<SecurityDispatcher>();
        services.TryAddSingleton<SecurityRuntime>();
        services.TryAddSingleton<SecurityEngine>();

        return services;
    }

    /// <summary>Registers the security engine, binding <see cref="SecurityEngineOptions"/> from configuration.</summary>
    /// <param name="services">The service collection to add registrations to.</param>
    /// <param name="configuration">The configuration to bind engine options from.</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance, to allow chaining.</returns>
    public static IServiceCollection AddSecurityEngine(
        this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var options = new SecurityEngineOptions();
        configuration.GetSection(SecurityConstants.ConfigurationSection).Bind(options);
        services.TryAddSingleton(options);

        return services.AddSecurityEngine();
    }

    /// <summary>
    /// Attaches the audit engine to the security event stream, so every decision, session and violation lands
    /// in the platform's immutable trail. Opt-in: a deployment that wants authorization without an audit trail
    /// simply does not call this.
    /// </summary>
    /// <param name="services">The service collection to add registrations to.</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance, to allow chaining.</returns>
    public static IServiceCollection AddSecurityAuditIntegration(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSecurityEngine();
        services.AddAuditEngine();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ISecurityEventSink, SecurityAuditBridge>());

        return services;
    }

    /// <summary>
    /// Attaches the monitoring engine to the security event stream, so authorization, sessions and violations
    /// are measured alongside everything else the platform does. Opt-in, for the same reason.
    /// </summary>
    /// <param name="services">The service collection to add registrations to.</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance, to allow chaining.</returns>
    public static IServiceCollection AddSecurityMonitoringIntegration(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSecurityEngine();
        services.AddMonitoringEngine();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ISecurityEventSink, SecurityMonitoringBridge>());

        return services;
    }
}
