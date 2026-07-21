using FactoryOS.Domain.Abstractions;
using FactoryOS.Domain.Time;
using FactoryOS.Plugins.Workflow.Tasks.Configuration;
using FactoryOS.Plugins.Workflow.Tasks.Diagnostics;
using FactoryOS.Plugins.Workflow.Tasks.Events;
using FactoryOS.Plugins.Workflow.Tasks.Execution;
using FactoryOS.Plugins.Workflow.Tasks.Localization;
using FactoryOS.Plugins.Workflow.Tasks.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registration entry point for the FactoryOS <b>Human Task engine</b> — the user-task runtime that runs on
/// top of the workflow engine. It registers the runtime, its default in-memory persistence, the assignment /
/// deadline / reminder / escalation engines, and the bridge that advances or cancels a workflow when a bound
/// task is completed, rejected or cancelled. The workflow engine is registered too (idempotently); the
/// workflow runtime and the forms engine are never modified.
/// </summary>
public static class HumanTaskEngineServiceCollectionExtensions
{
    /// <summary>Registers the human task engine and its default in-memory persistence.</summary>
    /// <param name="services">The service collection to add registrations to.</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance, to allow chaining.</returns>
    public static IServiceCollection AddHumanTaskEngine(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Tasks run on top of the workflow engine; register it (idempotently) so the bridge can resolve it.
        services.AddWorkflowEngine();

        services.TryAddSingleton(new HumanTaskEngineOptions());
        services.TryAddSingleton<IDateTimeProvider, SystemDateTimeProvider>();

        services.TryAddSingleton<IHumanTaskRepository, InMemoryHumanTaskRepository>();
        services.TryAddSingleton<IHumanTaskStore, InMemoryHumanTaskStore>();
        services.TryAddSingleton<IHumanTaskHistoryRepository, InMemoryHumanTaskHistoryRepository>();
        services.TryAddSingleton<IHumanTaskEventSink, InMemoryHumanTaskEventSink>();
        services.TryAddSingleton<IHumanTaskDirectory, InMemoryHumanTaskDirectory>();
        services.TryAddSingleton<IHumanTaskLocalizer, InMemoryHumanTaskLocalizer>();
        services.TryAddSingleton<IHumanTaskWorkflowBridge, HumanTaskWorkflowBridge>();

        services.TryAddSingleton<HumanTaskMetrics>();
        services.TryAddSingleton<HumanTaskExecutor>();
        services.TryAddSingleton<AssignmentResolver>();
        services.TryAddSingleton<DeadlineEngine>();
        services.TryAddSingleton<ReminderEngine>();
        services.TryAddSingleton<EscalationEngine>();
        services.TryAddSingleton<HumanTaskPermissionEvaluator>();

        services.TryAddSingleton<HumanTaskRuntime>();
        services.TryAddSingleton<TaskCompletionService>();
        services.TryAddSingleton<TaskCancellationService>();
        services.TryAddSingleton<HumanTaskEngine>();

        return services;
    }

    /// <summary>Registers the human task engine, binding <see cref="HumanTaskEngineOptions"/> from configuration.</summary>
    /// <param name="services">The service collection to add registrations to.</param>
    /// <param name="configuration">The configuration to bind engine options from.</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance, to allow chaining.</returns>
    public static IServiceCollection AddHumanTaskEngine(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var options = new HumanTaskEngineOptions();
        configuration.GetSection(HumanTaskConstants.ConfigurationSection).Bind(options);
        services.TryAddSingleton(options);

        return services.AddHumanTaskEngine();
    }
}
