using FactoryOS.Domain.Abstractions;
using FactoryOS.Domain.Time;
using FactoryOS.Plugins.Workflow.Approvals.Configuration;
using FactoryOS.Plugins.Workflow.Approvals.Diagnostics;
using FactoryOS.Plugins.Workflow.Approvals.Events;
using FactoryOS.Plugins.Workflow.Approvals.Execution;
using FactoryOS.Plugins.Workflow.Approvals.Localization;
using FactoryOS.Plugins.Workflow.Approvals.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registration entry point for the FactoryOS <b>Approval engine</b> — the enterprise approval runtime that
/// runs on top of the workflow engine. It registers the runtime, its default in-memory persistence, the
/// participant resolver, policy evaluator and deadline / reminder / escalation engines, and the bridge that
/// advances or cancels a workflow when a bound approval finishes. The workflow engine is registered too
/// (idempotently); the workflow runtime, the human task engine and the forms engine are never modified.
/// </summary>
public static class ApprovalEngineServiceCollectionExtensions
{
    /// <summary>Registers the approval engine and its default in-memory persistence.</summary>
    /// <param name="services">The service collection to add registrations to.</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance, to allow chaining.</returns>
    public static IServiceCollection AddApprovalEngine(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Approvals run on top of the workflow engine; register it (idempotently) so the bridge can resolve it.
        services.AddWorkflowEngine();

        services.TryAddSingleton(new ApprovalEngineOptions());
        services.TryAddSingleton<IDateTimeProvider, SystemDateTimeProvider>();

        services.TryAddSingleton<IApprovalRepository, InMemoryApprovalRepository>();
        services.TryAddSingleton<IApprovalStore, InMemoryApprovalStore>();
        services.TryAddSingleton<IApprovalHistoryRepository, InMemoryApprovalHistoryRepository>();
        services.TryAddSingleton<IApprovalEventSink, InMemoryApprovalEventSink>();
        services.TryAddSingleton<IApprovalLocalizer, InMemoryApprovalLocalizer>();
        services.TryAddSingleton<IApprovalWorkflowBridge, ApprovalWorkflowBridge>();

        services.TryAddSingleton<ApprovalMetrics>();
        services.TryAddSingleton<ApprovalExecutor>();
        services.TryAddSingleton<ParticipantResolver>();
        services.TryAddSingleton<ApprovalPolicyEvaluator>();
        services.TryAddSingleton<ApprovalDeadlineEngine>();
        services.TryAddSingleton<ApprovalReminderEngine>();
        services.TryAddSingleton<ApprovalEscalationEngine>();
        services.TryAddSingleton<ApprovalPermissionEvaluator>();

        services.TryAddSingleton<ApprovalCompletionService>();
        services.TryAddSingleton<ApprovalRuntime>();
        services.TryAddSingleton<ApprovalDecisionService>();
        services.TryAddSingleton<ApprovalCancellationService>();
        services.TryAddSingleton<ApprovalEngine>();

        return services;
    }

    /// <summary>Registers the approval engine, binding <see cref="ApprovalEngineOptions"/> from configuration.</summary>
    /// <param name="services">The service collection to add registrations to.</param>
    /// <param name="configuration">The configuration to bind engine options from.</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance, to allow chaining.</returns>
    public static IServiceCollection AddApprovalEngine(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var options = new ApprovalEngineOptions();
        configuration.GetSection(ApprovalConstants.ConfigurationSection).Bind(options);
        services.TryAddSingleton(options);

        return services.AddApprovalEngine();
    }
}
