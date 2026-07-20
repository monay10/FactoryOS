namespace FactoryOS.Configuration.Secrets;

/// <summary>
/// Resolves named secrets. Secrets are never stored in tenant configuration; configuration references
/// them by name (see <see cref="SecretExpander"/>) and a provider resolves the value at load time.
/// </summary>
public interface ISecretProvider
{
    /// <summary>Attempts to resolve a secret by name.</summary>
    /// <param name="name">The secret name.</param>
    /// <param name="value">The resolved value when found; otherwise <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the secret was resolved; otherwise <see langword="false"/>.</returns>
    bool TryGet(string name, out string? value);
}
