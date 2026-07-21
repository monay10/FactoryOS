namespace FactoryOS.Plugins.Workflow.Tasks.Configuration;

/// <summary>Stable constants for the human task engine.</summary>
public static class HumanTaskConstants
{
    /// <summary>The configuration section the engine options bind from.</summary>
    public const string ConfigurationSection = "Workflow:Tasks";

    /// <summary>The tenant used when a caller supplies none.</summary>
    public const string DefaultTenant = "default";

    /// <summary>The culture used when a task declares no localization.</summary>
    public const string DefaultCulture = "en";
}

/// <summary>
/// Runtime options for the human task engine (namespace <c>Tasks.Configuration</c>). These govern the task
/// runtime; they are independent of the workflow engine's and forms engine's own options.
/// </summary>
public sealed record HumanTaskEngineOptions
{
    /// <summary>
    /// Gets a value indicating whether creating a task from an in-memory definition object also registers it
    /// in the repository (so the task can be reloaded later).
    /// </summary>
    public bool AutoRegisterDefinitions { get; init; } = true;

    /// <summary>
    /// Gets a value indicating whether an expired task with no remaining escalation is expired automatically by
    /// the due-work pass. When <see langword="false"/> an overdue task stays waiting until acted on.
    /// </summary>
    public bool ExpireOverdueTasks { get; init; } = true;

    /// <summary>Gets the maximum number of tasks a single due-work pass processes.</summary>
    public int DueWorkBatchSize { get; init; } = 100;

    /// <summary>Gets the default culture used when a task declares no localization.</summary>
    public string DefaultCulture { get; init; } = HumanTaskConstants.DefaultCulture;
}

/// <summary>
/// The caller-supplied context a task is created within: the owning tenant and, optionally, the acting user.
/// The tenant is stamped onto the task so nothing crosses tenants.
/// </summary>
/// <param name="Tenant">The owning tenant.</param>
/// <param name="User">The acting user, if known.</param>
public sealed record HumanTaskContext(string Tenant, string? User = null)
{
    /// <summary>A context for the default tenant.</summary>
    public static HumanTaskContext Default { get; } = new(HumanTaskConstants.DefaultTenant);
}
