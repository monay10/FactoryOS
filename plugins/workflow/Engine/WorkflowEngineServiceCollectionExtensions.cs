using FactoryOS.Domain.Abstractions;
using FactoryOS.Domain.Time;
using FactoryOS.Plugins.Workflow.Engine.Configuration;
using FactoryOS.Plugins.Workflow.Engine.Events;
using FactoryOS.Plugins.Workflow.Engine.Execution;
using FactoryOS.Plugins.Workflow.Engine.Persistence;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registration entry point for the FactoryOS <b>Workflow engine</b> runtime — the definition repository,
/// instance store, executor, runtime, engine facade and timer scheduler. This is the stateful process
/// runtime; it is distinct from, and does not replace, the reactive workflow module's rule engine.
/// </summary>
public static class WorkflowEngineServiceCollectionExtensions
{
    /// <summary>Registers the workflow engine runtime and its default in-memory persistence.</summary>
    /// <param name="services">The service collection to add registrations to.</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance, to allow chaining.</returns>
    public static IServiceCollection AddWorkflowEngine(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton(new WorkflowEngineOptions());
        services.TryAddSingleton<IDateTimeProvider, SystemDateTimeProvider>();

        services.TryAddSingleton<IWorkflowRepository, InMemoryWorkflowRepository>();
        services.TryAddSingleton<IWorkflowStore, InMemoryWorkflowStore>();
        services.TryAddSingleton<IWorkflowEventSink, InMemoryWorkflowEventSink>();
        services.TryAddSingleton<IWorkflowServiceRegistry, WorkflowServiceRegistry>();

        services.TryAddSingleton<WorkflowExecutor>();
        services.TryAddSingleton<WorkflowRuntime>();
        services.TryAddSingleton<WorkflowEngine>();
        services.TryAddSingleton<WorkflowScheduler>();

        return services;
    }
}
