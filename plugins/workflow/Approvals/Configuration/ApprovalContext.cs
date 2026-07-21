namespace FactoryOS.Plugins.Workflow.Approvals.Configuration;

/// <summary>Stable constants for the approval engine.</summary>
public static class ApprovalConstants
{
    /// <summary>The configuration section the engine options bind from.</summary>
    public const string ConfigurationSection = "Workflow:Approvals";

    /// <summary>The tenant used when a caller supplies none.</summary>
    public const string DefaultTenant = "default";

    /// <summary>The culture used when an approval declares no localization.</summary>
    public const string DefaultCulture = "en";
}

/// <summary>
/// Runtime options for the approval engine (namespace <c>Approvals.Configuration</c>). These govern the
/// approval runtime; they are independent of the workflow, forms and human task engines' own options.
/// </summary>
public sealed record ApprovalEngineOptions
{
    /// <summary>
    /// Gets a value indicating whether starting an approval from an in-memory definition object also registers
    /// it in the repository (so the approval can be reloaded later).
    /// </summary>
    public bool AutoRegisterDefinitions { get; init; } = true;

    /// <summary>
    /// Gets a value indicating whether an approval that passes its deadline with no decision expires and
    /// completes its workflow activity down the rejection branch (a timeout is treated as a denial). When
    /// <see langword="false"/> an overdue approval stays open until decided.
    /// </summary>
    public bool ExpireOverdueApprovals { get; init; } = true;

    /// <summary>Gets the maximum number of approvals a single due-work pass processes.</summary>
    public int DueWorkBatchSize { get; init; } = 100;

    /// <summary>Gets the default culture used when an approval declares no localization.</summary>
    public string DefaultCulture { get; init; } = ApprovalConstants.DefaultCulture;
}

/// <summary>
/// The caller-supplied context an approval is started within: the owning tenant, an optional initiator, and
/// the values (workflow variables, form values) that resolve dynamic participants and auto-decision rules. The
/// tenant is stamped onto the approval so nothing crosses tenants.
/// </summary>
public sealed record ApprovalContext
{
    private static readonly IReadOnlyDictionary<string, object?> EmptyValues =
        new Dictionary<string, object?>(StringComparer.Ordinal);

    /// <summary>Initializes a new instance of the <see cref="ApprovalContext"/> record.</summary>
    /// <param name="tenant">The owning tenant.</param>
    /// <param name="initiatedBy">Who initiated the approval, if known.</param>
    /// <param name="values">The context values for dynamic resolution and rules.</param>
    public ApprovalContext(
        string tenant, string? initiatedBy = null, IReadOnlyDictionary<string, object?>? values = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);
        Tenant = tenant;
        InitiatedBy = initiatedBy;
        Values = values ?? EmptyValues;
    }

    /// <summary>Gets the owning tenant.</summary>
    public string Tenant { get; }

    /// <summary>Gets who initiated the approval, if known.</summary>
    public string? InitiatedBy { get; }

    /// <summary>Gets the context values used to resolve dynamic participants and rules.</summary>
    public IReadOnlyDictionary<string, object?> Values { get; }

    /// <summary>A context for the default tenant with no values.</summary>
    public static ApprovalContext Default { get; } = new(ApprovalConstants.DefaultTenant);
}
