namespace FactoryOS.Connectors.Runtime.Domain;

/// <summary>
/// Why an invocation failed, in a form the runtime can act on. The <see cref="Kind"/> is what decides whether
/// a retry could help and whether the circuit should count it, so classification happens once — at the edge
/// where the failure is actually understood — and never again.
/// </summary>
/// <param name="Code">A stable, machine-readable code.</param>
/// <param name="Message">A human-readable description.</param>
/// <param name="Kind">What sort of failure it is.</param>
public sealed record ConnectorError(string Code, string Message, ConnectorErrorKind Kind)
{
    /// <summary>Gets a value indicating whether a later attempt could plausibly succeed.</summary>
    /// <remarks>
    /// A refused call is not retryable here: an open circuit lasts for its break duration, which outlives any
    /// backoff the retry engine would apply, so retrying in place would only burn the attempts that the
    /// caller will need once the circuit closes.
    /// </remarks>
    public bool IsRetryable => Kind is ConnectorErrorKind.Transient or ConnectorErrorKind.Timeout
        or ConnectorErrorKind.Throttled;

    /// <summary>Gets a value indicating whether this failure says something about the external system's health.</summary>
    /// <remarks>
    /// A validation error or a refused permission is the caller's problem, not the remote system's. Counting
    /// those toward a circuit would let one badly-written client cut a whole factory off from its ERP.
    /// </remarks>
    public bool CountsAgainstHealth => Kind is ConnectorErrorKind.Transient or ConnectorErrorKind.Timeout
        or ConnectorErrorKind.Permanent or ConnectorErrorKind.Unknown;

    /// <summary>Creates a transient failure.</summary>
    /// <param name="code">The code.</param>
    /// <param name="message">The description.</param>
    /// <returns>The error.</returns>
    public static ConnectorError Transient(string code, string message) =>
        new(code, message, ConnectorErrorKind.Transient);

    /// <summary>Creates a timeout failure.</summary>
    /// <param name="code">The code.</param>
    /// <param name="message">The description.</param>
    /// <returns>The error.</returns>
    public static ConnectorError Timeout(string code, string message) =>
        new(code, message, ConnectorErrorKind.Timeout);

    /// <summary>Creates a permanent failure.</summary>
    /// <param name="code">The code.</param>
    /// <param name="message">The description.</param>
    /// <returns>The error.</returns>
    public static ConnectorError Permanent(string code, string message) =>
        new(code, message, ConnectorErrorKind.Permanent);

    /// <summary>Creates a validation failure.</summary>
    /// <param name="code">The code.</param>
    /// <param name="message">The description.</param>
    /// <returns>The error.</returns>
    public static ConnectorError Validation(string code, string message) =>
        new(code, message, ConnectorErrorKind.Validation);

    /// <summary>Creates a not-found failure.</summary>
    /// <param name="code">The code.</param>
    /// <param name="message">The description.</param>
    /// <returns>The error.</returns>
    public static ConnectorError NotFound(string code, string message) =>
        new(code, message, ConnectorErrorKind.NotFound);

    /// <summary>Creates an authorization failure.</summary>
    /// <param name="code">The code.</param>
    /// <param name="message">The description.</param>
    /// <returns>The error.</returns>
    public static ConnectorError Forbidden(string code, string message) =>
        new(code, message, ConnectorErrorKind.Forbidden);

    /// <summary>Creates an authentication failure.</summary>
    /// <param name="code">The code.</param>
    /// <param name="message">The description.</param>
    /// <returns>The error.</returns>
    public static ConnectorError Unauthorized(string code, string message) =>
        new(code, message, ConnectorErrorKind.Unauthorized);

    /// <summary>Creates a throttling failure.</summary>
    /// <param name="code">The code.</param>
    /// <param name="message">The description.</param>
    /// <returns>The error.</returns>
    public static ConnectorError Throttled(string code, string message) =>
        new(code, message, ConnectorErrorKind.Throttled);

    /// <summary>Creates a refusal because the circuit is open.</summary>
    /// <param name="code">The code.</param>
    /// <param name="message">The description.</param>
    /// <returns>The error.</returns>
    public static ConnectorError CircuitOpen(string code, string message) =>
        new(code, message, ConnectorErrorKind.CircuitOpen);

    /// <inheritdoc />
    public override string ToString() => $"{Code}: {Message}";
}

/// <summary>
/// The outcome of an invocation: whether it succeeded, what it produced or why it did not, and what it cost —
/// how long it took, how many attempts it needed and whether the answer came from the cache.
/// <para>
/// A failure is a value, not an exception. An ERP being down is an ordinary Tuesday in a factory, and the
/// caller that has to decide what to do about it should not have to catch something to find out.
/// </para>
/// </summary>
public sealed record ConnectorResponse
{
    /// <summary>Gets a value indicating whether the invocation succeeded.</summary>
    public required bool Succeeded { get; init; }

    /// <summary>Gets what the operation produced, when it succeeded.</summary>
    public object? Payload { get; init; }

    /// <summary>Gets why the invocation failed, when it did.</summary>
    public ConnectorError? Error { get; init; }

    /// <summary>Gets how long the whole invocation took, including every attempt and every wait between them.</summary>
    public TimeSpan Duration { get; init; }

    /// <summary>Gets how many attempts were made.</summary>
    public int Attempts { get; init; } = 1;

    /// <summary>Gets a value indicating whether the answer was served from the cache without a call being made.</summary>
    public bool FromCache { get; init; }

    /// <summary>Gets the identifiers tying the outcome back to the request.</summary>
    public ConnectorCorrelation Correlation { get; init; } = ConnectorCorrelation.Empty;

    /// <summary>Gets the values the operation reported alongside its payload.</summary>
    public IReadOnlyDictionary<string, string?> Metadata { get; init; } =
        new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Creates a successful response.</summary>
    /// <param name="payload">What the operation produced.</param>
    /// <returns>The response.</returns>
    public static ConnectorResponse Ok(object? payload = null) =>
        new() { Succeeded = true, Payload = payload };

    /// <summary>Creates a failed response.</summary>
    /// <param name="error">Why it failed.</param>
    /// <returns>The response.</returns>
    public static ConnectorResponse Failed(ConnectorError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new ConnectorResponse { Succeeded = false, Error = error };
    }

    /// <summary>Gets the typed payload, or <see langword="null"/> when it is absent or of another type.</summary>
    /// <typeparam name="T">The expected payload type.</typeparam>
    /// <returns>The payload, or <see langword="null"/>.</returns>
    public T? PayloadAs<T>()
        where T : class => Payload as T;

    /// <summary>Gets the outcome as a label, for metrics and audit.</summary>
    public string Outcome => Succeeded
        ? FromCache ? "cached" : "success"
        : Error?.Kind.ToString().ToLowerInvariant() ?? "failure";
}
