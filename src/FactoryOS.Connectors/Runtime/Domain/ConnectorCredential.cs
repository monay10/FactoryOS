using FactoryOS.Connectors.Runtime.Configuration;

namespace FactoryOS.Connectors.Runtime.Domain;

/// <summary>
/// A materialized secret. It is a value type whose <see cref="ToString"/> is masked, so a secret cannot reach
/// a log, an audit record, an exception message or a diagnostic dump through the ordinary act of formatting
/// something. Reading the real value takes the deliberate call to <see cref="Reveal"/>.
/// </summary>
public readonly struct ConnectorSecret : IEquatable<ConnectorSecret>
{
    private readonly string? _value;

    /// <summary>Initializes a new instance of the <see cref="ConnectorSecret"/> struct.</summary>
    /// <param name="value">The plaintext value.</param>
    public ConnectorSecret(string? value) => _value = value;

    /// <summary>Gets the empty secret.</summary>
    public static ConnectorSecret Empty => default;

    /// <summary>Gets a value indicating whether the secret holds anything.</summary>
    public bool HasValue => !string.IsNullOrEmpty(_value);

    /// <summary>Reveals the plaintext value.</summary>
    /// <returns>The plaintext, or an empty string when the secret is empty.</returns>
    public string Reveal() => _value ?? string.Empty;

    /// <inheritdoc />
    public bool Equals(ConnectorSecret other) => string.Equals(_value, other._value, StringComparison.Ordinal);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is ConnectorSecret other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => _value is null ? 0 : StringComparer.Ordinal.GetHashCode(_value);

    /// <summary>Determines whether two secrets hold the same value.</summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns><see langword="true"/> when they are equal.</returns>
    public static bool operator ==(ConnectorSecret left, ConnectorSecret right) => left.Equals(right);

    /// <summary>Determines whether two secrets hold different values.</summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns><see langword="true"/> when they differ.</returns>
    public static bool operator !=(ConnectorSecret left, ConnectorSecret right) => !left.Equals(right);

    /// <inheritdoc />
    public override string ToString() => HasValue ? ConnectorRuntimeConstants.SecretMask : string.Empty;
}

/// <summary>
/// A connector instance's credential <b>as stored</b>: its kind, its non-secret parts and a <b>reference</b>
/// to where the secret actually lives — never the secret itself.
/// <para>
/// Storing a reference rather than a value is what makes a connector configuration safe to commit, to export
/// and to show on a screen. Resolving the reference happens once, at invocation time, through the secret
/// resolver.
/// </para>
/// </summary>
public sealed record ConnectorCredential
{
    /// <summary>A credential for an external system that needs none.</summary>
    public static readonly ConnectorCredential None = new()
    {
        Key = "none",
        Kind = ConnectorCredentialKind.None,
    };

    /// <summary>Gets the key identifying the credential within its tenant.</summary>
    public required string Key { get; init; }

    /// <summary>Gets the shape of the credential.</summary>
    public required ConnectorCredentialKind Kind { get; init; }

    /// <summary>Gets the non-secret identity part (a user name, a client id, a certificate subject).</summary>
    public string? Identity { get; init; }

    /// <summary>
    /// Gets the reference the secret is resolved from: a <c>${secret:NAME}</c> placeholder resolved from the
    /// platform's secret source, or an <c>enc:</c> value decrypted by the connector secret protector.
    /// </summary>
    public string? SecretReference { get; init; }

    /// <summary>Gets a value indicating whether this credential needs a secret at all.</summary>
    public bool RequiresSecret => Kind != ConnectorCredentialKind.None;
}

/// <summary>
/// A credential once its secret has been resolved, handed to a connector for the length of one invocation.
/// The secret is wrapped so that formatting the credential cannot leak it.
/// </summary>
/// <param name="Key">The credential key.</param>
/// <param name="Kind">The shape of the credential.</param>
/// <param name="Identity">The non-secret identity part.</param>
/// <param name="Secret">The resolved secret.</param>
public sealed record ResolvedConnectorCredential(
    string Key,
    ConnectorCredentialKind Kind,
    string? Identity,
    ConnectorSecret Secret)
{
    /// <summary>Gets a resolved credential for an external system that needs none.</summary>
    public static ResolvedConnectorCredential None { get; } =
        new("none", ConnectorCredentialKind.None, null, ConnectorSecret.Empty);

    /// <summary>Gets a value indicating whether the credential is usable — it has whatever secret its kind needs.</summary>
    public bool IsComplete => Kind == ConnectorCredentialKind.None || Secret.HasValue;
}
