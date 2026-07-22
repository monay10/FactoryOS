using System.Globalization;

namespace FactoryOS.Connectors.Runtime.Domain;

/// <summary>
/// The identifiers that tie one connector invocation to the work that caused it: the business operation
/// (<see cref="CorrelationId"/>), the distributed trace (<see cref="TraceId"/>) and this single call
/// (<see cref="RequestId"/>).
/// <para>
/// All three travel from the request onto the response, the events, the audit record and the measurement.
/// A record of a failed ERP call that cannot be joined back to the work order it was for is one nobody can act on.
/// </para>
/// </summary>
/// <param name="CorrelationId">The business operation this call belongs to.</param>
/// <param name="TraceId">The distributed trace this call belongs to.</param>
/// <param name="RequestId">The identifier of this call alone.</param>
public sealed record ConnectorCorrelation(string? CorrelationId, string? TraceId, string? RequestId)
{
    /// <summary>An empty correlation, carrying nothing.</summary>
    public static readonly ConnectorCorrelation Empty = new(null, null, null);

    /// <summary>Creates a correlation from a business operation identifier alone.</summary>
    /// <param name="correlationId">The business operation identifier.</param>
    /// <returns>The correlation.</returns>
    public static ConnectorCorrelation For(string correlationId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);
        return new ConnectorCorrelation(correlationId, null, null);
    }

    /// <summary>Gets a value indicating whether the correlation carries any identifier.</summary>
    public bool IsEmpty => CorrelationId is null && TraceId is null && RequestId is null;

    /// <summary>Fills in whichever identifiers are missing, leaving any the caller supplied untouched.</summary>
    /// <param name="requestId">The identifier to use for this call when none was supplied.</param>
    /// <returns>A complete correlation.</returns>
    public ConnectorCorrelation Complete(string requestId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requestId);
        return new ConnectorCorrelation(
            CorrelationId ?? requestId,
            TraceId ?? CorrelationId ?? requestId,
            RequestId ?? requestId);
    }
}

/// <summary>
/// Who is asking. The runtime does not authenticate — the platform's identity layer does that — so a caller
/// arrives already carrying a subject, the permissions it was granted and the tenant it is acting in.
/// </summary>
/// <param name="Tenant">The tenant the caller is acting in.</param>
/// <param name="Subject">The caller's stable identifier.</param>
public sealed record ConnectorCaller(string Tenant, string Subject)
{
    /// <summary>Gets the permissions the caller holds, in <c>resource.action</c> form.</summary>
    public IReadOnlyList<string> Permissions { get; init; } = [];

    /// <summary>Gets a value indicating whether the caller was authenticated.</summary>
    public bool IsAuthenticated { get; init; } = true;

    /// <summary>Builds an unauthenticated caller, which holds nothing whatever permissions it names.</summary>
    /// <param name="tenant">The tenant the request names.</param>
    /// <returns>The caller.</returns>
    public static ConnectorCaller Anonymous(string tenant) =>
        new(tenant, "anonymous") { IsAuthenticated = false };

    /// <summary>Builds a caller holding a set of permissions.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <param name="subject">The caller's identifier.</param>
    /// <param name="permissions">The permissions it holds.</param>
    /// <returns>The caller.</returns>
    public static ConnectorCaller Holding(string tenant, string subject, params string[] permissions)
    {
        ArgumentNullException.ThrowIfNull(permissions);
        return new ConnectorCaller(tenant, subject) { Permissions = [.. permissions] };
    }
}

/// <summary>
/// One request to invoke a connector operation: which tenant's instance, which operation, with what
/// parameters, on whose behalf, and under which correlation.
/// </summary>
public sealed record ConnectorRequest
{
    /// <summary>Gets the tenant the request is made in.</summary>
    public required string Tenant { get; init; }

    /// <summary>Gets the key of the connector instance to invoke.</summary>
    public required string Instance { get; init; }

    /// <summary>Gets the operation to invoke.</summary>
    public required string Operation { get; init; }

    /// <summary>Gets the operation's parameters.</summary>
    public IReadOnlyDictionary<string, string?> Parameters { get; init; } =
        new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Gets the payload the operation acts on, when it takes one.</summary>
    public object? Payload { get; init; }

    /// <summary>Gets who is asking.</summary>
    public ConnectorCaller? Caller { get; init; }

    /// <summary>Gets the identifiers tying the call to the work that caused it.</summary>
    public ConnectorCorrelation Correlation { get; init; } = ConnectorCorrelation.Empty;

    /// <summary>Gets the deadline for one attempt, or <see langword="null"/> to use the operation's own.</summary>
    public TimeSpan? Timeout { get; init; }

    /// <summary>Reads a parameter.</summary>
    /// <param name="name">The parameter name.</param>
    /// <returns>The value, or <see langword="null"/> when absent.</returns>
    public string? Parameter(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return Parameters.TryGetValue(name, out var value) ? value : null;
    }

    /// <summary>Builds a request for an operation on an instance.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <param name="instance">The instance key.</param>
    /// <param name="operation">The operation name.</param>
    /// <returns>The request.</returns>
    public static ConnectorRequest For(string tenant, string instance, string operation)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);
        ArgumentException.ThrowIfNullOrWhiteSpace(instance);
        ArgumentException.ThrowIfNullOrWhiteSpace(operation);
        return new ConnectorRequest { Tenant = tenant, Instance = instance, Operation = operation };
    }

    /// <summary>Builds the cache identity of this request: the same call twice produces the same key.</summary>
    /// <returns>The cache key.</returns>
    public string CacheKey()
    {
        var parameters = string.Join(
            ';',
            Parameters
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => string.Create(CultureInfo.InvariantCulture, $"{pair.Key}={pair.Value}")));

        return string.Create(CultureInfo.InvariantCulture, $"{Tenant}|{Instance}|{Operation}|{parameters}");
    }
}
