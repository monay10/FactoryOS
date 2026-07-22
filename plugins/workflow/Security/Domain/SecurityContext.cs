namespace FactoryOS.Plugins.Workflow.Security.Domain;

/// <summary>
/// The identifiers tying a security decision to the operation that asked for it, carried through verbatim from
/// whatever raised the request. An audit record of a denial that cannot be joined to the request it refused is
/// a record nobody can act on.
/// </summary>
/// <param name="CorrelationId">Groups everything produced by one logical operation.</param>
/// <param name="TraceId">The distributed trace the operation belonged to.</param>
/// <param name="RequestId">The inbound request that triggered it.</param>
public sealed record SecurityCorrelation(
    string? CorrelationId = null, string? TraceId = null, string? RequestId = null)
{
    /// <summary>An empty correlation, for operations with no ambient context.</summary>
    public static SecurityCorrelation None { get; } = new();

    /// <summary>Creates a correlation that only groups by a logical operation id.</summary>
    /// <param name="correlationId">The correlation id.</param>
    /// <returns>The correlation.</returns>
    public static SecurityCorrelation For(string correlationId) => new(correlationId);
}

/// <summary>
/// Everything a decision is made from: who is asking, what they want to do, to what, where the request came
/// from and when. One object, assembled once per request, so that every rule in a policy sees exactly the same
/// facts — a rule that could observe a different "now" than the rule beside it would make a policy's outcome
/// depend on how long it took to evaluate.
/// </summary>
public sealed class SecurityContext
{
    private readonly Dictionary<string, string> _environment;

    /// <summary>Initializes a new instance of the <see cref="SecurityContext"/> class.</summary>
    /// <param name="principal">Who is asking.</param>
    /// <param name="resource">What is being acted on.</param>
    /// <param name="action">What is being done.</param>
    /// <param name="scope">The breadth the request belongs to.</param>
    /// <param name="requestedOnUtc">When the request was made — the single "now" every rule sees.</param>
    /// <param name="correlation">The identifiers tying the request to its operation.</param>
    /// <param name="networkAddress">Where the request came from.</param>
    /// <param name="environment">Any other facts rules may reason about.</param>
    public SecurityContext(
        SecurityPrincipal principal,
        SecurityResource resource,
        SecurityAction action,
        SecurityScope scope,
        DateTimeOffset requestedOnUtc,
        SecurityCorrelation? correlation = null,
        string? networkAddress = null,
        IReadOnlyDictionary<string, string>? environment = null)
    {
        ArgumentNullException.ThrowIfNull(principal);
        ArgumentNullException.ThrowIfNull(resource);
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(scope);

        Principal = principal;
        Resource = resource;
        Action = action;
        Scope = scope;
        RequestedOnUtc = requestedOnUtc;
        Correlation = correlation ?? SecurityCorrelation.None;
        NetworkAddress = networkAddress;
        _environment = environment is null
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(environment, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Gets who is asking.</summary>
    public SecurityPrincipal Principal { get; }

    /// <summary>Gets what is being acted on.</summary>
    public SecurityResource Resource { get; }

    /// <summary>Gets what is being done.</summary>
    public SecurityAction Action { get; }

    /// <summary>Gets the breadth the request belongs to.</summary>
    public SecurityScope Scope { get; }

    /// <summary>Gets when the request was made.</summary>
    public DateTimeOffset RequestedOnUtc { get; }

    /// <summary>Gets the identifiers tying the request to its operation.</summary>
    public SecurityCorrelation Correlation { get; }

    /// <summary>Gets where the request came from, when it is known.</summary>
    public string? NetworkAddress { get; }

    /// <summary>Gets any other facts rules may reason about.</summary>
    public IReadOnlyDictionary<string, string> Environment => _environment;

    /// <summary>Gets the permission this request is asking for: the resource type and the action.</summary>
    public SecurityPermission RequestedPermission => SecurityPermission.Of(Resource.Type, Action.Name);

    /// <summary>Gets an environment value, or <see langword="null"/> when it is not present.</summary>
    /// <param name="name">The name.</param>
    /// <returns>The value, or <see langword="null"/>.</returns>
    public string? this[string name] => _environment.TryGetValue(name, out var value) ? value : null;

    /// <summary>
    /// Gets a value indicating whether the principal belongs to the tenant the request names. This is checked
    /// before any policy runs and cannot be granted around: per the Constitution there is no code path that
    /// reads or writes across tenants, so it is a structural gate rather than something a rule can allow.
    /// </summary>
    public bool IsSameTenant =>
        string.Equals(Principal.Tenant, Scope.Tenant, StringComparison.Ordinal);
}
