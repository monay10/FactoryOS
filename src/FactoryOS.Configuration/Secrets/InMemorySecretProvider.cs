namespace FactoryOS.Configuration.Secrets;

/// <summary>An in-memory <see cref="ISecretProvider"/>, primarily for tests and local development.</summary>
public sealed class InMemorySecretProvider : ISecretProvider
{
    private readonly Dictionary<string, string> _secrets;

    /// <summary>Initializes a new instance of the <see cref="InMemorySecretProvider"/> class.</summary>
    /// <param name="secrets">The secrets to serve.</param>
    public InMemorySecretProvider(IReadOnlyDictionary<string, string> secrets)
    {
        ArgumentNullException.ThrowIfNull(secrets);
        _secrets = new Dictionary<string, string>(secrets, StringComparer.Ordinal);
    }

    /// <inheritdoc />
    public bool TryGet(string name, out string? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (_secrets.TryGetValue(name, out var resolved))
        {
            value = resolved;
            return true;
        }

        value = null;
        return false;
    }
}
