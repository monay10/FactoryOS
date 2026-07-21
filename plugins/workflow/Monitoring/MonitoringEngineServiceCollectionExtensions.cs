using FactoryOS.Domain.Abstractions;
using FactoryOS.Domain.Time;
using FactoryOS.Plugins.Forms.Engine.Events;
using FactoryOS.Plugins.Workflow.Approvals.Events;
using FactoryOS.Plugins.Workflow.Audit.Events;
using FactoryOS.Plugins.Workflow.Engine.Events;
using FactoryOS.Plugins.Workflow.Monitoring.Bridge;
using FactoryOS.Plugins.Workflow.Monitoring.Collections;
using FactoryOS.Plugins.Workflow.Monitoring.Configuration;
using FactoryOS.Plugins.Workflow.Monitoring.Diagnostics;
using FactoryOS.Plugins.Workflow.Monitoring.Events;
using FactoryOS.Plugins.Workflow.Monitoring.Execution;
using FactoryOS.Plugins.Workflow.Monitoring.Health;
using FactoryOS.Plugins.Workflow.Monitoring.Localization;
using FactoryOS.Plugins.Workflow.Monitoring.Persistence;
using FactoryOS.Plugins.Workflow.Notifications.Events;
using FactoryOS.Plugins.Workflow.SLA.Events;
using FactoryOS.Plugins.Workflow.Tasks.Events;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registration entry point for the FactoryOS <b>Monitoring engine</b> — the platform's observability layer.
/// It registers the collector, sampler, aggregator, retention manager, threshold and alert evaluators, the
/// health runtime and its twelve checks, the thirteen metric collections, and the bridges that turn each
/// engine's events into measurements.
/// <para>
/// Monitoring sits at the outermost layer, below everything it observes, and the dependency arrow points one
/// way only: engines publish, monitoring reads. No engine is modified, and where a seam allows a single
/// consumer, the bridge wraps whatever is already registered and appends itself — so notifications and audit
/// keep receiving exactly what they did before.
/// </para>
/// </summary>
public static class MonitoringEngineServiceCollectionExtensions
{
    /// <summary>Registers the monitoring engine and wires it to every engine whose events it measures.</summary>
    /// <param name="services">The service collection to add registrations to.</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance, to allow chaining.</returns>
    public static IServiceCollection AddMonitoringEngine(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Wiring the bridges is not idempotent by itself, so the whole registration is guarded.
        if (services.Any(descriptor => descriptor.ServiceType == typeof(MonitoringEngine)))
        {
            return services;
        }

        // Compose the engines this engine measures first, so their seams exist before they are wrapped.
        // Audit brings notifications — and through them workflow, forms, human tasks and approvals — plus SLA.
        services.AddAuditEngine();

        services.TryAddSingleton(new MonitoringEngineOptions());
        services.TryAddSingleton<IDateTimeProvider, SystemDateTimeProvider>();

        services.TryAddSingleton<IMetricRepository, InMemoryMetricRepository>();
        services.TryAddSingleton<IMetricStore, InMemoryMetricStore>();
        services.TryAddSingleton<IHealthRepository, InMemoryHealthRepository>();
        services.TryAddSingleton<IHealthStore, InMemoryHealthStore>();
        services.TryAddSingleton<IMonitoringPermissionStore, InMemoryMonitoringPermissionStore>();
        services.TryAddSingleton<IMonitoringLocalizer, InMemoryMonitoringLocalizer>();

        // The monitoring event seam fans out, so an exporter and a dashboard feed can both observe it.
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IMonitoringEventSink, InMemoryMonitoringEventSink>());

        services.TryAddSingleton<MonitoringMetrics>();
        services.TryAddSingleton<MetricSampler>();
        services.TryAddSingleton<MetricAggregator>();
        services.TryAddSingleton<MetricCollector>();
        services.TryAddSingleton<MetricRetentionManager>();
        services.TryAddSingleton<ThresholdEvaluator>();
        services.TryAddSingleton<AlertEvaluator>();
        services.TryAddSingleton<MetricSearchService>();
        services.TryAddSingleton<HealthRegistry>();
        services.TryAddSingleton<HealthCheckExecutor>();
        services.TryAddSingleton<HealthEngine>();
        services.TryAddSingleton<MonitoringDispatcher>();
        services.TryAddSingleton<MonitoringPermissionEvaluator>();
        services.TryAddSingleton<MonitoringRuntime>();

        // The catalogue and the health checks are registered as the engine is built, so a container that
        // resolves the engine always has something to measure against — never an empty registry.
        services.TryAddSingleton(provider =>
        {
            var engine = new MonitoringEngine(
                provider.GetRequiredService<MonitoringRuntime>(),
                provider.GetRequiredService<IMetricRepository>(),
                provider.GetRequiredService<HealthRegistry>(),
                provider.GetRequiredService<MonitoringPermissionEvaluator>(),
                provider.GetRequiredService<MonitoringMetrics>());

            MetricCatalog.RegisterAll(provider.GetRequiredService<IMetricRepository>());
            foreach (var (check, probe) in PlatformHealthChecks.All())
            {
                engine.RegisterHealthCheck(check, probe);
            }

            return engine;
        });

        services.TryAddSingleton<SlaMetricsBridge>();
        services.TryAddSingleton<AuditMetricsBridge>();

        // The SLA and audit seams already fan out, so monitoring simply joins them.
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ISlaEventSink, SlaMetricsBridge>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IAuditEventSink, AuditMetricsBridge>());

        // The other five seams allow a single consumer, so wrap whatever is registered and append monitoring.
        Wrap<IWorkflowEventSink>(services, (provider, inner) => new WorkflowMetricsBridge(
            provider.GetRequiredService<MonitoringEngine>(),
            provider.GetRequiredService<MonitoringMetrics>(),
            inner));
        Wrap<IFormEventSink>(services, (provider, inner) => new FormsMetricsBridge(
            provider.GetRequiredService<MonitoringEngine>(),
            provider.GetRequiredService<MonitoringMetrics>(),
            inner));
        Wrap<IHumanTaskEventSink>(services, (provider, inner) => new HumanTaskMetricsBridge(
            provider.GetRequiredService<MonitoringEngine>(),
            provider.GetRequiredService<MonitoringMetrics>(),
            inner));
        Wrap<IApprovalEventSink>(services, (provider, inner) => new ApprovalMetricsBridge(
            provider.GetRequiredService<MonitoringEngine>(),
            provider.GetRequiredService<MonitoringMetrics>(),
            inner));
        Wrap<INotificationEventSink>(services, (provider, inner) => new NotificationMetricsBridge(
            provider.GetRequiredService<MonitoringEngine>(),
            provider.GetRequiredService<MonitoringMetrics>(),
            inner));

        return services;
    }

    /// <summary>Registers the monitoring engine, binding <see cref="MonitoringEngineOptions"/> from configuration.</summary>
    /// <param name="services">The service collection to add registrations to.</param>
    /// <param name="configuration">The configuration to bind engine options from.</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance, to allow chaining.</returns>
    public static IServiceCollection AddMonitoringEngine(
        this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var options = new MonitoringEngineOptions();
        configuration.GetSection(MonitoringConstants.ConfigurationSection).Bind(options);
        services.TryAddSingleton(options);

        return services.AddMonitoringEngine();
    }

    /// <summary>
    /// Replaces a single-consumer event seam with a bridge that forwards to the consumer already registered
    /// (if any) and then measures the event. The previous registration is resolved exactly as the container
    /// would have resolved it, so a subscriber registered as a shared singleton stays the same instance.
    /// </summary>
    /// <typeparam name="TSink">The event sink interface.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="bridge">Builds the bridge around the previous consumer.</param>
    private static void Wrap<TSink>(IServiceCollection services, Func<IServiceProvider, TSink?, TSink> bridge)
        where TSink : class
    {
        var existing = services.LastOrDefault(descriptor => descriptor.ServiceType == typeof(TSink));
        if (existing is not null)
        {
            services.Remove(existing);
        }

        services.AddSingleton(provider =>
            bridge(provider, existing is null ? null : ResolveExisting<TSink>(provider, existing)));
    }

    private static TSink ResolveExisting<TSink>(IServiceProvider provider, ServiceDescriptor descriptor)
        where TSink : class
    {
        if (descriptor.ImplementationInstance is TSink instance)
        {
            return instance;
        }

        if (descriptor.ImplementationFactory is { } factory)
        {
            return (TSink)factory(provider);
        }

        return (TSink)ActivatorUtilities.CreateInstance(provider, descriptor.ImplementationType!);
    }
}
