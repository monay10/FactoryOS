namespace FactoryOS.Plugins.Workflow.Audit.Domain;

/// <summary>
/// What a caller submits to be audited: the description of something that happened, before the audit engine
/// seals it. The engine turns an entry into an immutable <see cref="AuditRecord"/> by stamping it with a
/// per-tenant sequence number and linking it into the hash chain. Keeping the two types apart is what makes
/// "records are immutable" enforceable: an entry is freely constructed, a record can only be sealed.
/// </summary>
public sealed record AuditEntry
{
    /// <summary>Gets which part of the platform the entry came from.</summary>
    public required AuditCategory Category { get; init; }

    /// <summary>Gets the verb describing what happened.</summary>
    public required AuditAction Action { get; init; }

    /// <summary>Gets what the entry is about.</summary>
    public required AuditTarget Target { get; init; }

    /// <summary>Gets the breadth the entry belongs to (tenant, and optionally organization and module).</summary>
    public required AuditScope Scope { get; init; }

    /// <summary>Gets who performed the operation; defaults to the platform itself.</summary>
    public AuditActor Actor { get; init; } = AuditActor.System;

    /// <summary>Gets how much attention the entry deserves.</summary>
    public AuditSeverity Severity { get; init; } = AuditSeverity.Info;

    /// <summary>Gets whether the operation succeeded.</summary>
    public AuditResult Result { get; init; } = AuditResult.Success;

    /// <summary>Gets the identifiers tying the entry to its request, trace and session.</summary>
    public AuditCorrelation Correlation { get; init; } = AuditCorrelation.None;

    /// <summary>Gets the precise source event type name, when the entry came from an event.</summary>
    public string EventType { get; init; } = string.Empty;

    /// <summary>Gets the human-readable description.</summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>Gets the before-and-after state, for entries that describe a change.</summary>
    public AuditSnapshot? Snapshot { get; init; }

    /// <summary>Gets additional key-value context.</summary>
    public IReadOnlyDictionary<string, string> Metadata { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>Gets the labels used to slice the trail.</summary>
    public IReadOnlyList<AuditTag> Tags { get; init; } = [];

    /// <summary>Gets when the operation happened; the engine stamps the recording time separately.</summary>
    public DateTimeOffset? OccurredOnUtc { get; init; }
}

/// <summary>
/// Ready-made entries for the platform sources that have no event-sink seam of their own — sign-in and
/// sign-out, access decisions, configuration changes, connector calls and plugin lifecycle. These are the
/// audit API those parts of the platform call directly, rather than through a subscriber.
/// </summary>
public static class AuditEntries
{
    /// <summary>Creates an entry for a successful or failed sign-in.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <param name="userId">The user signing in.</param>
    /// <param name="succeeded">Whether the sign-in succeeded.</param>
    /// <param name="correlation">The correlation identifiers.</param>
    /// <param name="message">An optional description.</param>
    /// <returns>The entry.</returns>
    public static AuditEntry SignIn(
        string tenant, string userId, bool succeeded, AuditCorrelation? correlation = null, string? message = null) =>
        new()
        {
            Category = AuditCategory.Authentication,
            Action = AuditAction.SignedIn,
            Target = new AuditTarget(AuditTargetType.User, userId),
            Scope = AuditScope.ForTenant(tenant),
            Actor = AuditActor.User(userId),
            Severity = succeeded ? AuditSeverity.Info : AuditSeverity.Warning,
            Result = succeeded ? AuditResult.Success : AuditResult.Failure,
            Correlation = correlation ?? AuditCorrelation.None,
            EventType = nameof(SignIn),
            Message = message ?? (succeeded ? $"{userId} signed in." : $"{userId} failed to sign in."),
        };

    /// <summary>Creates an entry for a sign-out.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <param name="userId">The user signing out.</param>
    /// <param name="correlation">The correlation identifiers.</param>
    /// <returns>The entry.</returns>
    public static AuditEntry SignOut(string tenant, string userId, AuditCorrelation? correlation = null) =>
        new()
        {
            Category = AuditCategory.Authentication,
            Action = AuditAction.SignedOut,
            Target = new AuditTarget(AuditTargetType.User, userId),
            Scope = AuditScope.ForTenant(tenant),
            Actor = AuditActor.User(userId),
            Correlation = correlation ?? AuditCorrelation.None,
            EventType = nameof(SignOut),
            Message = $"{userId} signed out.",
        };

    /// <summary>Creates an entry for an access decision.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <param name="userId">The principal whose access was decided.</param>
    /// <param name="resource">The resource access was requested to.</param>
    /// <param name="granted">Whether access was granted.</param>
    /// <param name="correlation">The correlation identifiers.</param>
    /// <returns>The entry.</returns>
    public static AuditEntry AccessDecision(
        string tenant, string userId, string resource, bool granted, AuditCorrelation? correlation = null) =>
        new()
        {
            Category = AuditCategory.Authorization,
            Action = granted ? AuditAction.AccessGranted : AuditAction.AccessDenied,
            Target = new AuditTarget(AuditTargetType.Role, resource),
            Scope = AuditScope.ForTenant(tenant),
            Actor = AuditActor.User(userId),
            // A denial is security-relevant and should stand out in the trail.
            Severity = granted ? AuditSeverity.Info : AuditSeverity.Critical,
            Result = granted ? AuditResult.Success : AuditResult.Denied,
            Correlation = correlation ?? AuditCorrelation.None,
            EventType = nameof(AccessDecision),
            Message = granted
                ? $"{userId} was granted access to {resource}."
                : $"{userId} was denied access to {resource}.",
        };

    /// <summary>Creates an entry for a configuration change, carrying the before-and-after state.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <param name="section">The configuration section that changed.</param>
    /// <param name="actor">Who changed it.</param>
    /// <param name="snapshot">The before-and-after state.</param>
    /// <param name="correlation">The correlation identifiers.</param>
    /// <returns>The entry.</returns>
    public static AuditEntry ConfigurationChanged(
        string tenant,
        string section,
        AuditActor actor,
        AuditSnapshot snapshot,
        AuditCorrelation? correlation = null)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return new AuditEntry
        {
            Category = AuditCategory.Configuration,
            Action = AuditAction.Changed,
            Target = new AuditTarget(AuditTargetType.Configuration, section),
            Scope = AuditScope.ForTenant(tenant),
            Actor = actor,
            Severity = AuditSeverity.Notice,
            Correlation = correlation ?? AuditCorrelation.None,
            EventType = nameof(ConfigurationChanged),
            Snapshot = snapshot,
            Message = $"Configuration '{section}' changed ({string.Join(", ", snapshot.ChangedFields())}).",
        };
    }

    /// <summary>Creates an entry for a connector operation against an outside system.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <param name="connectorKey">The connector.</param>
    /// <param name="operation">The operation performed.</param>
    /// <param name="succeeded">Whether it succeeded.</param>
    /// <param name="correlation">The correlation identifiers.</param>
    /// <returns>The entry.</returns>
    public static AuditEntry ConnectorOperation(
        string tenant,
        string connectorKey,
        string operation,
        bool succeeded,
        AuditCorrelation? correlation = null) =>
        new()
        {
            Category = AuditCategory.Connector,
            Action = AuditAction.Executed,
            Target = new AuditTarget(AuditTargetType.Connector, connectorKey),
            Scope = AuditScope.ForTenant(tenant),
            Actor = new AuditActor(connectorKey, AuditActorKind.External, connectorKey),
            Severity = succeeded ? AuditSeverity.Info : AuditSeverity.Warning,
            Result = succeeded ? AuditResult.Success : AuditResult.Failure,
            Correlation = correlation ?? AuditCorrelation.None,
            EventType = nameof(ConnectorOperation),
            Message = $"Connector '{connectorKey}' executed '{operation}'.",
        };

    /// <summary>Creates an entry for a plugin lifecycle operation.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <param name="pluginKey">The plugin.</param>
    /// <param name="operation">The lifecycle operation performed.</param>
    /// <param name="actor">Who performed it.</param>
    /// <param name="correlation">The correlation identifiers.</param>
    /// <returns>The entry.</returns>
    public static AuditEntry PluginOperation(
        string tenant,
        string pluginKey,
        string operation,
        AuditActor? actor = null,
        AuditCorrelation? correlation = null) =>
        new()
        {
            Category = AuditCategory.Plugin,
            Action = AuditAction.Executed,
            Target = new AuditTarget(AuditTargetType.Plugin, pluginKey),
            Scope = AuditScope.ForTenant(tenant),
            Actor = actor ?? AuditActor.System,
            Severity = AuditSeverity.Notice,
            Correlation = correlation ?? AuditCorrelation.None,
            EventType = nameof(PluginOperation),
            Message = $"Plugin '{pluginKey}' {operation}.",
        };
}
