using FactoryOS.Connectors.Runtime.Domain;

namespace FactoryOS.Connectors.Runtime.Execution;

/// <summary>
/// Everything one invocation needs, carried through the pipeline: what was asked, of which instance, under
/// which definition and operation, with which resilience — plus the state each stage contributes as the call
/// travels (its credential, its session, how many attempts it has taken).
/// <para>
/// The request, instance, definition and operation are fixed at the start. A middleware that could rewrite
/// which instance is being invoked would make the authorization decision made two stages earlier a lie.
/// </para>
/// </summary>
public sealed class ConnectorInvocation
{
    private readonly Dictionary<string, object?> _items = new(StringComparer.Ordinal);

    /// <summary>Initializes a new instance of the <see cref="ConnectorInvocation"/> class.</summary>
    /// <param name="request">What was asked.</param>
    /// <param name="instance">The instance being invoked.</param>
    /// <param name="definition">The definition it activates.</param>
    /// <param name="operation">The operation being invoked.</param>
    /// <param name="resilience">The resilience this invocation runs under.</param>
    /// <param name="startedUtc">When the invocation started.</param>
    public ConnectorInvocation(
        ConnectorRequest request,
        ConnectorInstance instance,
        ConnectorDefinition definition,
        ConnectorOperation operation,
        ConnectorResiliencePolicy resilience,
        DateTimeOffset startedUtc)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(resilience);

        Request = request;
        Instance = instance;
        Definition = definition;
        Operation = operation;
        Resilience = resilience;
        StartedUtc = startedUtc;
        Correlation = request.Correlation;
    }

    /// <summary>Gets what was asked.</summary>
    public ConnectorRequest Request { get; }

    /// <summary>Gets the instance being invoked.</summary>
    public ConnectorInstance Instance { get; }

    /// <summary>Gets the definition the instance activates.</summary>
    public ConnectorDefinition Definition { get; }

    /// <summary>Gets the operation being invoked.</summary>
    public ConnectorOperation Operation { get; }

    /// <summary>Gets the resilience this invocation runs under.</summary>
    public ConnectorResiliencePolicy Resilience { get; }

    /// <summary>Gets when the invocation started.</summary>
    public DateTimeOffset StartedUtc { get; }

    /// <summary>Gets the tenant the invocation is made in.</summary>
    public string Tenant => Instance.Tenant;

    /// <summary>Gets the identifiers tying the invocation to the work that caused it.</summary>
    public ConnectorCorrelation Correlation { get; internal set; }

    /// <summary>Gets how many attempts have been made against the external system.</summary>
    public int Attempts { get; private set; }

    /// <summary>Gets the credential resolved for this invocation, once authentication has run.</summary>
    public ResolvedConnectorCredential? Credential { get; internal set; }

    /// <summary>Gets the session this invocation is using, when one was opened.</summary>
    public ConnectorSession? Session { get; internal set; }

    /// <summary>Gets the deadline for one attempt.</summary>
    /// <param name="fallback">The runtime default, used when neither the request nor the operation names one.</param>
    /// <returns>The deadline.</returns>
    public TimeSpan TimeoutOr(TimeSpan fallback) => Request.Timeout ?? Operation.Timeout ?? fallback;

    /// <summary>Records that another attempt is being made.</summary>
    /// <returns>The attempt number, counting the first as one.</returns>
    public int BeginAttempt() => ++Attempts;

    /// <summary>Stores a value for a later stage of the pipeline.</summary>
    /// <param name="key">The item key.</param>
    /// <param name="value">The value.</param>
    public void Set(string key, object? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        _items[key] = value;
    }

    /// <summary>Reads a value an earlier stage stored.</summary>
    /// <typeparam name="T">The expected value type.</typeparam>
    /// <param name="key">The item key.</param>
    /// <returns>The value, or <see langword="default"/> when absent or of another type.</returns>
    public T? Get<T>(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return _items.TryGetValue(key, out var value) && value is T typed ? typed : default;
    }

    /// <summary>Builds the telemetry describing how this invocation ended.</summary>
    /// <param name="response">The response it produced.</param>
    /// <returns>The telemetry.</returns>
    public ConnectorTelemetry Telemetry(ConnectorResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);

        return new ConnectorTelemetry(
            Tenant, Instance.Key, Definition.Key, Operation.Name, Correlation)
        {
            StartedUtc = StartedUtc,
            Duration = response.Duration,
            Attempts = Math.Max(1, response.Attempts),
            Succeeded = response.Succeeded,
            FromCache = response.FromCache,
            Error = response.Error,
        };
    }
}

/// <summary>The next stage of the connector pipeline.</summary>
/// <param name="invocation">The invocation travelling through the pipeline.</param>
/// <param name="cancellationToken">A token to cancel the invocation.</param>
/// <returns>The response the rest of the pipeline produced.</returns>
public delegate Task<ConnectorResponse> ConnectorInvocationDelegate(
    ConnectorInvocation invocation, CancellationToken cancellationToken);

/// <summary>
/// One stage of the connector pipeline. Each stage wraps the rest, so a stage may inspect the invocation on
/// the way in, short-circuit it, and inspect the response on the way out.
/// </summary>
public interface IConnectorMiddleware
{
    /// <summary>Gets the stage's name, as it appears in diagnostics.</summary>
    string Name { get; }

    /// <summary>Gets the stage's position; lower runs further out, wrapping everything above it.</summary>
    int Order { get; }

    /// <summary>Runs the stage.</summary>
    /// <param name="invocation">The invocation.</param>
    /// <param name="next">The rest of the pipeline.</param>
    /// <param name="cancellationToken">A token to cancel the invocation.</param>
    /// <returns>The response.</returns>
    Task<ConnectorResponse> InvokeAsync(
        ConnectorInvocation invocation, ConnectorInvocationDelegate next, CancellationToken cancellationToken);
}
