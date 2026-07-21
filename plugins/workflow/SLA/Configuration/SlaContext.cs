namespace FactoryOS.Plugins.Workflow.SLA.Configuration;

/// <summary>Stable constants for the SLA engine.</summary>
public static class SlaConstants
{
    /// <summary>The configuration section the engine options bind from.</summary>
    public const string ConfigurationSection = "Workflow:Sla";

    /// <summary>The tenant used when a caller supplies none.</summary>
    public const string DefaultTenant = "default";

    /// <summary>The culture used when an SLA declares no localization.</summary>
    public const string DefaultCulture = "en";
}

/// <summary>
/// Runtime options for the SLA engine (namespace <c>SLA.Configuration</c>). These govern the SLA runtime only;
/// they are independent of the workflow, forms, human task, approval and notification engines' own options. The
/// SLA engine tracks work through target references and never modifies those engines.
/// </summary>
public sealed record SlaEngineOptions
{
    /// <summary>
    /// Gets a value indicating whether starting an SLA from an in-memory definition object also registers it in
    /// the repository (so the SLA can be reloaded later).
    /// </summary>
    public bool AutoRegisterDefinitions { get; init; } = true;

    /// <summary>Gets the maximum number of SLAs a single due-work pass processes.</summary>
    public int DueWorkBatchSize { get; init; } = 200;

    /// <summary>
    /// Gets a value indicating whether an SLA whose hard timeout passes finishes as
    /// <see cref="Domain.SlaOutcome.TimedOut"/>. When <see langword="false"/> the timeout is recorded but the
    /// SLA stays open until the tracked work finishes or it is cancelled.
    /// </summary>
    public bool TimeOutOverdueSlas { get; init; } = true;

    /// <summary>Gets the default culture used when an SLA declares no localization.</summary>
    public string DefaultCulture { get; init; } = SlaConstants.DefaultCulture;
}

/// <summary>
/// The caller-supplied context an SLA is started within: the owning tenant, who started it, and the culture its
/// messages resolve in. The tenant is stamped onto the SLA so nothing crosses tenants.
/// </summary>
public sealed record SlaContext
{
    /// <summary>Initializes a new instance of the <see cref="SlaContext"/> record.</summary>
    /// <param name="tenant">The owning tenant.</param>
    /// <param name="startedBy">Who started the SLA, if known.</param>
    /// <param name="culture">The culture messages resolve in.</param>
    public SlaContext(string tenant, string? startedBy = null, string? culture = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);
        Tenant = tenant;
        StartedBy = startedBy;
        Culture = string.IsNullOrWhiteSpace(culture) ? SlaConstants.DefaultCulture : culture;
    }

    /// <summary>Gets the owning tenant.</summary>
    public string Tenant { get; }

    /// <summary>Gets who started the SLA, if known.</summary>
    public string? StartedBy { get; }

    /// <summary>Gets the culture messages resolve in.</summary>
    public string Culture { get; }

    /// <summary>A context for the default tenant.</summary>
    public static SlaContext Default { get; } = new(SlaConstants.DefaultTenant);
}
