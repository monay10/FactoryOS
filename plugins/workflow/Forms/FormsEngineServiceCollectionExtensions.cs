using FactoryOS.Domain.Abstractions;
using FactoryOS.Domain.Time;
using FactoryOS.Plugins.Forms.Engine.Configuration;
using FactoryOS.Plugins.Forms.Engine.Diagnostics;
using FactoryOS.Plugins.Forms.Engine.Events;
using FactoryOS.Plugins.Forms.Engine.Execution;
using FactoryOS.Plugins.Forms.Engine.Localization;
using FactoryOS.Plugins.Forms.Engine.Persistence;
using FactoryOS.Plugins.Forms.Engine.Rendering;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registration entry point for the FactoryOS <b>Forms engine</b> — the dynamic form runtime that renders,
/// validates and submits forms on top of the workflow engine. It registers the runtime, its default in-memory
/// persistence, and the bridge that advances a workflow when a form bound to an activity is submitted. The
/// workflow engine is registered too (idempotently); the workflow runtime itself is never modified.
/// </summary>
public static class FormsEngineServiceCollectionExtensions
{
    /// <summary>Registers the forms engine and its default in-memory persistence.</summary>
    /// <param name="services">The service collection to add registrations to.</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance, to allow chaining.</returns>
    public static IServiceCollection AddFormsEngine(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Forms run on top of the workflow engine; register it (idempotently) so the bridge can resolve it.
        services.AddWorkflowEngine();

        services.TryAddSingleton(new FormEngineOptions());
        services.TryAddSingleton<IDateTimeProvider, SystemDateTimeProvider>();

        services.TryAddSingleton<IFormRepository, InMemoryFormRepository>();
        services.TryAddSingleton<IFormStore, InMemoryFormStore>();
        services.TryAddSingleton<IFormVersionRepository, InMemoryFormVersionRepository>();
        services.TryAddSingleton<IFormSubmissionRepository, InMemoryFormSubmissionRepository>();
        services.TryAddSingleton<IFormEventSink, InMemoryFormEventSink>();
        services.TryAddSingleton<IFormLocalizer, InMemoryFormLocalizer>();
        services.TryAddSingleton<IFormWorkflowBridge, WorkflowFormBridge>();

        services.TryAddSingleton<FormMetrics>();
        services.TryAddSingleton<RuleEvaluator>();
        services.TryAddSingleton<ValidationEngine>();
        services.TryAddSingleton<FormExecutor>();
        services.TryAddSingleton<LayoutEngine>();
        services.TryAddSingleton<FormRenderer>();
        services.TryAddSingleton<FormPermissionEvaluator>();

        services.TryAddSingleton<FormRuntime>();
        services.TryAddSingleton<DraftService>();
        services.TryAddSingleton<SubmissionService>();
        services.TryAddSingleton<FormEngine>();

        return services;
    }

    /// <summary>Registers the forms engine, binding <see cref="FormEngineOptions"/> from configuration.</summary>
    /// <param name="services">The service collection to add registrations to.</param>
    /// <param name="configuration">The configuration to bind engine options from.</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance, to allow chaining.</returns>
    public static IServiceCollection AddFormsEngine(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var options = new FormEngineOptions();
        configuration.GetSection(FormConstants.ConfigurationSection).Bind(options);
        services.TryAddSingleton(options);

        return services.AddFormsEngine();
    }
}
