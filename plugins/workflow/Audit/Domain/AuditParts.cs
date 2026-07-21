namespace FactoryOS.Plugins.Workflow.Audit.Domain;

/// <summary>Who performed an audited operation.</summary>
/// <param name="Id">The principal's id (a user id, service name or plugin key).</param>
/// <param name="Kind">What kind of principal it is.</param>
/// <param name="DisplayName">A human-readable name, when known.</param>
public sealed record AuditActor(string Id, AuditActorKind Kind = AuditActorKind.User, string? DisplayName = null)
{
    /// <summary>The platform acting with no user behind the action.</summary>
    public static AuditActor System { get; } = new("system", AuditActorKind.System, "System");

    /// <summary>Creates an actor for a human user.</summary>
    /// <param name="userId">The user id.</param>
    /// <param name="displayName">A human-readable name.</param>
    /// <returns>The actor.</returns>
    public static AuditActor User(string userId, string? displayName = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        return new AuditActor(userId, AuditActorKind.User, displayName);
    }

    /// <summary>Creates an actor for a background service or scheduler.</summary>
    /// <param name="serviceName">The service name.</param>
    /// <returns>The actor.</returns>
    public static AuditActor Service(string serviceName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);
        return new AuditActor(serviceName, AuditActorKind.Service, serviceName);
    }
}

/// <summary>What an audited operation was about.</summary>
/// <param name="Type">The kind of entity.</param>
/// <param name="Key">The entity's definition key or name.</param>
/// <param name="Id">The entity's instance id, when it has one.</param>
public sealed record AuditTarget(AuditTargetType Type, string Key, string? Id = null)
{
    /// <summary>Creates a target from an entity kind, key and identifier.</summary>
    /// <param name="type">The entity kind.</param>
    /// <param name="key">The definition key or name.</param>
    /// <param name="id">The instance id.</param>
    /// <returns>The target.</returns>
    public static AuditTarget Of(AuditTargetType type, string key, Guid id) => new(type, key, id.ToString());
}

/// <summary>
/// The breadth an audit record belongs to. Every record is scoped to a tenant; the optional organization and
/// module narrow it further so a plant manager can read their own site's trail without seeing the whole tenant.
/// </summary>
/// <param name="Tenant">The owning tenant.</param>
/// <param name="Organization">The organization or site, when the record belongs to one.</param>
/// <param name="Module">The module or plugin key the record came from.</param>
public sealed record AuditScope(string Tenant, string? Organization = null, string? Module = null)
{
    /// <summary>Creates a scope covering a whole tenant.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <returns>The scope.</returns>
    public static AuditScope ForTenant(string tenant)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);
        return new AuditScope(tenant);
    }
}

/// <summary>
/// The identifiers that tie an audit record to the request, trace and session it came from, so a single user
/// action can be reconstructed across every engine it touched. They are carried through verbatim — the audit
/// engine never regenerates them.
/// </summary>
/// <param name="CorrelationId">Groups every record produced by one logical operation.</param>
/// <param name="TraceId">The distributed trace the operation belonged to.</param>
/// <param name="SessionId">The user session the operation belonged to.</param>
/// <param name="RequestId">The inbound request that triggered the operation.</param>
/// <param name="CausationId">The specific event or command that caused this one.</param>
public sealed record AuditCorrelation(
    string? CorrelationId = null,
    string? TraceId = null,
    string? SessionId = null,
    string? RequestId = null,
    string? CausationId = null)
{
    /// <summary>An empty correlation, for operations with no ambient context.</summary>
    public static AuditCorrelation None { get; } = new();

    /// <summary>Creates a correlation that only groups by a logical operation id.</summary>
    /// <param name="correlationId">The correlation id.</param>
    /// <returns>The correlation.</returns>
    public static AuditCorrelation For(string correlationId) => new(correlationId);
}

/// <summary>
/// The before-and-after state of a change, so an update is auditable in substance and not just in name. Values
/// are captured as text because an audit trail must stay readable long after the type that produced it changed.
/// </summary>
public sealed class AuditSnapshot
{
    private readonly Dictionary<string, string?> _before;
    private readonly Dictionary<string, string?> _after;

    /// <summary>Initializes a new instance of the <see cref="AuditSnapshot"/> class.</summary>
    /// <param name="before">The state before the change.</param>
    /// <param name="after">The state after the change.</param>
    public AuditSnapshot(
        IReadOnlyDictionary<string, string?>? before = null,
        IReadOnlyDictionary<string, string?>? after = null)
    {
        _before = before is null
            ? new Dictionary<string, string?>(StringComparer.Ordinal)
            : new Dictionary<string, string?>(before, StringComparer.Ordinal);
        _after = after is null
            ? new Dictionary<string, string?>(StringComparer.Ordinal)
            : new Dictionary<string, string?>(after, StringComparer.Ordinal);
    }

    /// <summary>Gets the state before the change.</summary>
    public IReadOnlyDictionary<string, string?> Before => _before;

    /// <summary>Gets the state after the change.</summary>
    public IReadOnlyDictionary<string, string?> After => _after;

    /// <summary>Gets the names of the fields whose value differs between before and after.</summary>
    /// <returns>The changed field names, ordered.</returns>
    public IReadOnlyList<string> ChangedFields() =>
        _before.Keys.Union(_after.Keys, StringComparer.Ordinal)
            .Where(field => !string.Equals(Value(_before, field), Value(_after, field), StringComparison.Ordinal))
            .OrderBy(field => field, StringComparer.Ordinal)
            .ToArray();

    /// <summary>Renders the snapshot into the stable text the hash chain covers.</summary>
    /// <returns>The canonical text.</returns>
    public string ToCanonicalString()
    {
        var before = string.Join(",", _before.OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => $"{pair.Key}={pair.Value}"));
        var after = string.Join(",", _after.OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => $"{pair.Key}={pair.Value}"));
        return $"before[{before}]after[{after}]";
    }

    private static string? Value(Dictionary<string, string?> map, string field) =>
        map.TryGetValue(field, out var value) ? value : null;
}

/// <summary>A free-form label attached to an audit record, used to slice the trail in searches and reports.</summary>
/// <param name="Name">The tag name.</param>
public sealed record AuditTag(string Name)
{
    /// <summary>Creates a tag, normalising it to lower case so searches are case-insensitive by construction.</summary>
    /// <param name="name">The tag name.</param>
    /// <returns>The tag.</returns>
    public static AuditTag Of(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return new AuditTag(name.Trim().ToLowerInvariant());
    }
}
