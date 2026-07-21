namespace FactoryOS.Plugins.Workflow.Engine.Configuration;

/// <summary>Stable constants for the workflow engine runtime.</summary>
public static class WorkflowConstants
{
    /// <summary>The configuration section the engine options bind from.</summary>
    public const string ConfigurationSection = "Workflow:Engine";

    /// <summary>The tenant used when a caller supplies none.</summary>
    public const string DefaultTenant = "default";

    /// <summary>The default number of instances a single scheduler pass fires timers for.</summary>
    public const int DefaultSchedulerBatchSize = 100;
}

/// <summary>
/// Runtime options for the workflow engine (namespace <c>Engine.Configuration</c>). Kept distinct from the
/// reactive workflow module's rule options of the same name in the parent namespace; these govern the
/// process runtime, not the alert-to-action rules.
/// </summary>
public sealed record WorkflowEngineOptions
{
    /// <summary>
    /// Gets a value indicating whether starting a workflow from an in-memory definition object also registers
    /// it in the repository (so the running instance can be resumed later).
    /// </summary>
    public bool AutoRegisterDefinitions { get; init; } = true;

    /// <summary>Gets the maximum number of instances a single scheduler pass processes.</summary>
    public int SchedulerBatchSize { get; init; } = WorkflowConstants.DefaultSchedulerBatchSize;
}

/// <summary>
/// The caller-supplied context a workflow starts within: the owning tenant and, optionally, who initiated it
/// and a correlation id. The tenant is stamped onto the instance so no execution can cross tenants.
/// </summary>
/// <param name="Tenant">The owning tenant.</param>
/// <param name="InitiatedBy">Who initiated the workflow, if known.</param>
/// <param name="CorrelationId">An optional correlation id linking the workflow to its trigger.</param>
public sealed record WorkflowContext(
    string Tenant, string? InitiatedBy = null, string? CorrelationId = null)
{
    /// <summary>A context for the default tenant.</summary>
    public static WorkflowContext Default { get; } = new(WorkflowConstants.DefaultTenant);
}
