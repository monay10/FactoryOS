using FactoryOS.Domain.Abstractions;
using FactoryOS.Domain.Time;
using FactoryOS.Plugins.Workflow.SLA.Configuration;
using FactoryOS.Plugins.Workflow.SLA.Diagnostics;
using FactoryOS.Plugins.Workflow.SLA.Events;
using FactoryOS.Plugins.Workflow.SLA.Execution;
using FactoryOS.Plugins.Workflow.SLA.Integration;
using FactoryOS.Plugins.Workflow.SLA.Localization;
using FactoryOS.Plugins.Workflow.SLA.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registration entry point for the FactoryOS <b>SLA engine</b> — the shared service-level-agreement
/// infrastructure the workflow, human task and approval engines' work can be tracked against. It registers the
/// runtime, its default in-memory persistence and calendar repository, the business-time calculator and
/// scheduler, and the deadline / reminder / escalation / timeout engines.
/// <para>
/// The engine is deliberately standalone: an SLA is attached to a target by the caller, so nothing here
/// subscribes to, references or modifies the workflow, human task, approval, forms or notification engines.
/// Forwarding SLA events to notifications is a separate opt-in — see
/// <see cref="AddSlaNotificationIntegration(IServiceCollection)"/>.
/// </para>
/// </summary>
public static class SlaEngineServiceCollectionExtensions
{
    /// <summary>Registers the SLA engine and its default in-memory infrastructure.</summary>
    /// <param name="services">The service collection to add registrations to.</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance, to allow chaining.</returns>
    public static IServiceCollection AddSlaEngine(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton(new SlaEngineOptions());
        services.TryAddSingleton<IDateTimeProvider, SystemDateTimeProvider>();

        services.TryAddSingleton<ISlaRepository, InMemorySlaRepository>();
        services.TryAddSingleton<ISlaStore, InMemorySlaStore>();
        services.TryAddSingleton<ISlaHistoryRepository, InMemorySlaHistoryRepository>();
        services.TryAddSingleton<ISlaCalendarRepository, InMemorySlaCalendarRepository>();
        services.TryAddSingleton<ISlaLocalizer, InMemorySlaLocalizer>();

        // The SLA event seam fans out: every registered sink sees the stream, so a recorder and a bridge can
        // both observe it. TryAddEnumerable keeps repeated registration idempotent.
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ISlaEventSink, InMemorySlaEventSink>());

        services.TryAddSingleton<SlaMetrics>();
        services.TryAddSingleton<BusinessTimeCalculator>();
        services.TryAddSingleton<CalendarEngine>();
        services.TryAddSingleton<SlaScheduler>();
        services.TryAddSingleton<DeadlineEngine>();
        services.TryAddSingleton<ReminderEngine>();
        services.TryAddSingleton<EscalationEngine>();
        services.TryAddSingleton<TimeoutEngine>();
        services.TryAddSingleton<SlaEvaluator>();
        services.TryAddSingleton<SlaPermissionEvaluator>();
        services.TryAddSingleton<SlaRuntime>();
        services.TryAddSingleton<SlaEngine>();

        return services;
    }

    /// <summary>Registers the SLA engine, binding <see cref="SlaEngineOptions"/> from configuration.</summary>
    /// <param name="services">The service collection to add registrations to.</param>
    /// <param name="configuration">The configuration to bind engine options from.</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance, to allow chaining.</returns>
    public static IServiceCollection AddSlaEngine(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var options = new SlaEngineOptions();
        configuration.GetSection(SlaConstants.ConfigurationSection).Bind(options);
        services.TryAddSingleton(options);

        return services.AddSlaEngine();
    }

    /// <summary>
    /// Adds the opt-in bridge that forwards the SLA events needing human attention (reminders, escalations,
    /// missed deadlines and timeouts) to the notification engine, and composes both engines. Without this call
    /// the SLA engine runs with no dependency on notifications at all.
    /// </summary>
    /// <param name="services">The service collection to add registrations to.</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance, to allow chaining.</returns>
    public static IServiceCollection AddSlaNotificationIntegration(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSlaEngine();
        services.AddNotificationEngine();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ISlaEventSink, SlaNotificationBridge>());

        return services;
    }
}
