using FactoryOS.Plugins.Workflow.Audit.Domain;

namespace FactoryOS.Plugins.Workflow.Audit.Configuration;

/// <summary>Stable constants for the audit engine.</summary>
public static class AuditConstants
{
    /// <summary>The configuration section the engine options bind from.</summary>
    public const string ConfigurationSection = "Workflow:Audit";

    /// <summary>The tenant used when a source event carries none.</summary>
    public const string DefaultTenant = "default";

    /// <summary>The culture used when an audit message declares no localization.</summary>
    public const string DefaultCulture = "en";
}

/// <summary>
/// Runtime options for the audit engine (namespace <c>Audit.Configuration</c>). These govern the audit runtime
/// only; they are independent of every engine whose events it records, and none of those engines is modified.
/// </summary>
public sealed record AuditEngineOptions
{
    /// <summary>
    /// Gets the lowest severity that is recorded. Anything below it is filtered out before it reaches the
    /// chain, which keeps a noisy source from burying the trail.
    /// </summary>
    public AuditSeverity MinimumSeverity { get; init; } = AuditSeverity.Info;

    /// <summary>
    /// Gets the categories that are not recorded at all. Empty means every category is recorded.
    /// </summary>
    public IReadOnlyList<AuditCategory> ExcludedCategories { get; init; } = [];

    /// <summary>Gets the maximum number of records a single retention or archive pass moves.</summary>
    public int MaintenanceBatchSize { get; init; } = 500;

    /// <summary>
    /// Gets a value indicating whether the engine's own housekeeping (archiving, expiry, export) is itself
    /// audited. On by default: an audit trail that does not record who exported it is not much of an audit trail.
    /// </summary>
    public bool AuditOwnOperations { get; init; } = true;

    /// <summary>Gets the default culture used when an audit message declares no localization.</summary>
    public string DefaultCulture { get; init; } = AuditConstants.DefaultCulture;
}
