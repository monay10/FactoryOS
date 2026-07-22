using System.Collections.Concurrent;
using FactoryOS.Connectors.Framework.Security;
using FactoryOS.Connectors.Runtime.Configuration;
using FactoryOS.Connectors.Runtime.Domain;
using FactoryOS.Connectors.Runtime.Persistence;

namespace FactoryOS.Connectors.Runtime.Security;

/// <summary>
/// Where a <c>${secret:NAME}</c> reference is looked up. Implementations reach a vault, a Kubernetes secret,
/// an environment variable — anywhere that is not the connector's configuration file.
/// </summary>
public interface IConnectorSecretSource
{
    /// <summary>Looks up a secret by name.</summary>
    /// <param name="name">The secret name.</param>
    /// <returns>The value, or <see langword="null"/> when this source does not hold it.</returns>
    string? Find(string name);
}

/// <summary>
/// The default <see cref="IConnectorSecretSource"/>, reading process environment variables — the mechanism
/// every container orchestrator already has, so a development host needs nothing installed and a production
/// host needs nothing in the repository.
/// </summary>
public sealed class EnvironmentConnectorSecretSource : IConnectorSecretSource
{
    /// <inheritdoc />
    public string? Find(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var value = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrEmpty(value) ? null : value;
    }
}

/// <summary>
/// An in-memory <see cref="IConnectorSecretSource"/> for hosts that supply secrets programmatically, and for
/// tests that must prove resolution without touching the machine's environment.
/// </summary>
public sealed class InMemoryConnectorSecretSource : IConnectorSecretSource
{
    private readonly ConcurrentDictionary<string, string> _secrets = new(StringComparer.Ordinal);

    /// <summary>Stores a secret under a name.</summary>
    /// <param name="name">The secret name.</param>
    /// <param name="value">The value.</param>
    public void Set(string name, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(value);
        _secrets[name] = value;
    }

    /// <summary>Forgets a secret.</summary>
    /// <param name="name">The secret name.</param>
    /// <returns><see langword="true"/> when a secret was forgotten.</returns>
    public bool Remove(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return _secrets.TryRemove(name, out _);
    }

    /// <inheritdoc />
    public string? Find(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return _secrets.TryGetValue(name, out var value) ? value : null;
    }
}

/// <summary>
/// Turns a credential <b>reference</b> into a usable credential.
/// <para>
/// Two forms are understood, and both keep the secret out of the connector's configuration. A
/// <c>${secret:NAME}</c> placeholder is looked up in the secret sources — a vault, the orchestrator, the
/// environment. An <c>enc:</c> value is decrypted with the connector framework's existing AES-GCM protector,
/// which is reused rather than reimplemented: a second crypto path is a second thing to get wrong.
/// </para>
/// <para>
/// Anything else is treated as plaintext and resolves unchanged, because a development host with a local
/// test database should not need a vault. That is why <c>sample.config.json</c> carries placeholders and
/// never values.
/// </para>
/// </summary>
public sealed class ConnectorSecretResolver
{
    private readonly IReadOnlyList<IConnectorSecretSource> _sources;
    private readonly IConnectorSecretProtector _protector;
    private readonly IConnectorCredentialStore _credentials;

    /// <summary>Initializes a new instance of the <see cref="ConnectorSecretResolver"/> class.</summary>
    /// <param name="sources">The sources a <c>${secret:NAME}</c> reference is looked up in, in order.</param>
    /// <param name="protector">The protector that decrypts <c>enc:</c> values.</param>
    /// <param name="credentials">The credential reference store.</param>
    public ConnectorSecretResolver(
        IEnumerable<IConnectorSecretSource> sources,
        IConnectorSecretProtector protector,
        IConnectorCredentialStore credentials)
    {
        ArgumentNullException.ThrowIfNull(sources);
        ArgumentNullException.ThrowIfNull(protector);
        ArgumentNullException.ThrowIfNull(credentials);

        _sources = [.. sources];
        _protector = protector;
        _credentials = credentials;
    }

    /// <summary>Determines whether a value is a secret reference rather than a literal.</summary>
    /// <param name="value">The value to inspect.</param>
    /// <returns><see langword="true"/> when the value is a placeholder or an encrypted value.</returns>
    public bool IsReference(string? value) =>
        value is not null
        && (IsPlaceholder(value) || _protector.IsProtected(value));

    /// <summary>Resolves a single value.</summary>
    /// <param name="value">The value, reference or literal.</param>
    /// <returns>The resolved secret; empty when a placeholder names nothing any source holds.</returns>
    public ConnectorSecret Resolve(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return ConnectorSecret.Empty;
        }

        if (IsPlaceholder(value))
        {
            var name = value[
                ConnectorRuntimeConstants.SecretPlaceholderPrefix.Length..
                ^ConnectorRuntimeConstants.SecretPlaceholderSuffix.Length].Trim();

            if (name.Length == 0)
            {
                return ConnectorSecret.Empty;
            }

            foreach (var source in _sources)
            {
                if (source.Find(name) is { } found)
                {
                    return new ConnectorSecret(found);
                }
            }

            return ConnectorSecret.Empty;
        }

        return new ConnectorSecret(_protector.IsProtected(value) ? _protector.Unprotect(value) : value);
    }

    /// <summary>Resolves a credential reference into a usable credential.</summary>
    /// <param name="credential">The credential reference.</param>
    /// <returns>The resolved credential; its secret is empty when the reference could not be resolved.</returns>
    public ResolvedConnectorCredential Resolve(ConnectorCredential credential)
    {
        ArgumentNullException.ThrowIfNull(credential);

        return credential.Kind == ConnectorCredentialKind.None
            ? ResolvedConnectorCredential.None
            : new ResolvedConnectorCredential(
                credential.Key, credential.Kind, credential.Identity, Resolve(credential.SecretReference));
    }

    /// <summary>Resolves the credential a tenant's instance presents.</summary>
    /// <param name="instance">The instance.</param>
    /// <returns>
    /// The resolved credential. The instance's own credential reference is used when it names a secret;
    /// otherwise the tenant's credential store is consulted by key, so several instances can share one
    /// managed credential without each repeating the reference.
    /// </returns>
    public ResolvedConnectorCredential ResolveFor(ConnectorInstance instance)
    {
        ArgumentNullException.ThrowIfNull(instance);

        var credential = instance.Credential;
        if (credential.Kind == ConnectorCredentialKind.None)
        {
            return ResolvedConnectorCredential.None;
        }

        if (credential.SecretReference is null && _credentials.Find(instance.Tenant, credential.Key) is { } stored)
        {
            credential = stored;
        }

        return Resolve(credential);
    }

    private static bool IsPlaceholder(string value) =>
        value.StartsWith(ConnectorRuntimeConstants.SecretPlaceholderPrefix, StringComparison.Ordinal)
        && value.EndsWith(ConnectorRuntimeConstants.SecretPlaceholderSuffix, StringComparison.Ordinal)
        && value.Length > ConnectorRuntimeConstants.SecretPlaceholderPrefix.Length
        + ConnectorRuntimeConstants.SecretPlaceholderSuffix.Length - 1;
}
