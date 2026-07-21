using FactoryOS.Domain.Abstractions;
using FactoryOS.Domain.Time;
using FactoryOS.Plugins.Forms.Engine.Events;
using FactoryOS.Plugins.Workflow.Approvals.Events;
using FactoryOS.Plugins.Workflow.Audit.Configuration;
using FactoryOS.Plugins.Workflow.Audit.Diagnostics;
using FactoryOS.Plugins.Workflow.Audit.Events;
using FactoryOS.Plugins.Workflow.Audit.Execution;
using FactoryOS.Plugins.Workflow.Audit.Localization;
using FactoryOS.Plugins.Workflow.Audit.Persistence;
using FactoryOS.Plugins.Workflow.Audit.Sources;
using FactoryOS.Plugins.Workflow.Engine.Events;
using FactoryOS.Plugins.Workflow.Notifications.Events;
using FactoryOS.Plugins.Workflow.SLA.Events;
using FactoryOS.Plugins.Workflow.Tasks.Events;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registration entry point for the FactoryOS <b>Audit engine</b> — the immutable, hash-chained trail of
/// everything the platform does. It registers the recorder, chain verifier, filter, policy evaluator, archive
/// and retention managers, search and export services, and the subscribers that turn each engine's events into
/// audit records.
/// <para>
/// Audit sits at the bottom of the stack: it consumes the events the engines above it publish and writes
/// nothing back. None of those engines is modified — where a seam allows only one consumer, the audit layer
/// wraps the existing registration in a composite and appends itself, so every prior consumer keeps working.
/// </para>
/// </summary>
public static class AuditEngineServiceCollectionExtensions
{
    /// <summary>Registers the audit engine and wires it to every engine whose events it records.</summary>
    /// <param name="services">The service collection to add registrations to.</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance, to allow chaining.</returns>
    public static IServiceCollection AddAuditEngine(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Wiring the composites is not idempotent by itself, so the whole registration is guarded.
        if (services.Any(descriptor => descriptor.ServiceType == typeof(AuditEngine)))
        {
            return services;
        }

        // Compose the engines this engine audits first, so their own sinks exist before they are wrapped.
        // Notifications brings the workflow, forms, human task and approval engines with it.
        services.AddNotificationEngine();
        services.AddSlaEngine();

        services.TryAddSingleton(new AuditEngineOptions());
        services.TryAddSingleton<IDateTimeProvider, SystemDateTimeProvider>();

        services.TryAddSingleton<IAuditRepository, InMemoryAuditRepository>();
        services.TryAddSingleton<IAuditStore, InMemoryAuditStore>();
        services.TryAddSingleton<IAuditArchiveRepository, InMemoryAuditArchiveRepository>();
        services.TryAddSingleton<IAuditPermissionStore, InMemoryAuditPermissionStore>();
        services.TryAddSingleton<IAuditLocalizer, InMemoryAuditLocalizer>();

        // The audit event seam fans out, so a recorder and, say, a SIEM forwarder can both observe it.
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IAuditEventSink, InMemoryAuditEventSink>());

        services.TryAddSingleton<AuditMetrics>();
        services.TryAddSingleton<AuditRecorder>();
        services.TryAddSingleton<AuditChainVerifier>();
        services.TryAddSingleton<AuditFilter>();
        services.TryAddSingleton<AuditPolicyEvaluator>();
        services.TryAddSingleton<AuditArchiveManager>();
        services.TryAddSingleton<AuditRetentionManager>();
        services.TryAddSingleton<AuditSearchService>();
        services.TryAddSingleton<AuditExportService>();
        services.TryAddSingleton<AuditDispatcher>();
        services.TryAddSingleton<AuditPermissionEvaluator>();
        services.TryAddSingleton<AuditRuntime>();
        services.TryAddSingleton<AuditEngine>();

        services.TryAddSingleton<WorkflowAuditSubscriber>();
        services.TryAddSingleton<FormsAuditSubscriber>();
        services.TryAddSingleton<HumanTaskAuditSubscriber>();
        services.TryAddSingleton<ApprovalAuditSubscriber>();
        services.TryAddSingleton<NotificationAuditSubscriber>();
        services.TryAddSingleton<SlaAuditSubscriber>();

        // The SLA seam already fans out, so audit simply joins it.
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ISlaEventSink, SlaAuditSubscriber>());

        // The other five seams allow a single consumer, so wrap whatever is registered and append audit.
        FanOut<IWorkflowEventSink>(
            services,
            sinks => new CompositeWorkflowEventSink(sinks),
            provider => provider.GetRequiredService<WorkflowAuditSubscriber>());
        FanOut<IFormEventSink>(
            services,
            sinks => new CompositeFormEventSink(sinks),
            provider => provider.GetRequiredService<FormsAuditSubscriber>());
        FanOut<IHumanTaskEventSink>(
            services,
            sinks => new CompositeHumanTaskEventSink(sinks),
            provider => provider.GetRequiredService<HumanTaskAuditSubscriber>());
        FanOut<IApprovalEventSink>(
            services,
            sinks => new CompositeApprovalEventSink(sinks),
            provider => provider.GetRequiredService<ApprovalAuditSubscriber>());
        FanOut<INotificationEventSink>(
            services,
            sinks => new CompositeNotificationEventSink(sinks),
            provider => provider.GetRequiredService<NotificationAuditSubscriber>());

        return services;
    }

    /// <summary>Registers the audit engine, binding <see cref="AuditEngineOptions"/> from configuration.</summary>
    /// <param name="services">The service collection to add registrations to.</param>
    /// <param name="configuration">The configuration to bind engine options from.</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance, to allow chaining.</returns>
    public static IServiceCollection AddAuditEngine(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var options = new AuditEngineOptions();
        configuration.GetSection(AuditConstants.ConfigurationSection).Bind(options);
        services.TryAddSingleton(options);

        return services.AddAuditEngine();
    }

    /// <summary>
    /// Replaces a single-consumer event seam with a composite that publishes to the consumer already registered
    /// (if any) and then to the audit subscriber. The previous registration is resolved exactly as the container
    /// would have resolved it, so a subscriber registered as a shared singleton stays the same instance.
    /// </summary>
    /// <typeparam name="TSink">The event sink interface.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="composite">Builds the composite from the ordered sinks.</param>
    /// <param name="auditSink">Resolves the audit subscriber.</param>
    private static void FanOut<TSink>(
        IServiceCollection services,
        Func<IEnumerable<TSink>, TSink> composite,
        Func<IServiceProvider, TSink> auditSink)
        where TSink : class
    {
        var existing = services.LastOrDefault(descriptor => descriptor.ServiceType == typeof(TSink));
        if (existing is not null)
        {
            services.Remove(existing);
        }

        services.AddSingleton(provider =>
        {
            var sinks = new List<TSink>(2);
            if (existing is not null)
            {
                sinks.Add(ResolveExisting<TSink>(provider, existing));
            }

            sinks.Add(auditSink(provider));
            return composite(sinks);
        });
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
