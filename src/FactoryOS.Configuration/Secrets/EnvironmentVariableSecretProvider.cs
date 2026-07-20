namespace FactoryOS.Configuration.Secrets;

/// <summary>
/// An <see cref="ISecretProvider"/> that resolves secrets from environment variables, optionally under
/// a fixed prefix (e.g. <c>FACTORYOS_SECRET_</c>). This keeps secrets out of configuration files.
/// </summary>
public sealed class EnvironmentVariableSecretProvider : ISecretProvider
{
    private readonly string _prefix;

    /// <summary>Initializes a new instance of the <see cref="EnvironmentVariableSecretProvider"/> class.</summary>
    /// <param name="prefix">A prefix prepended to each secret name before lookup. Defaults to <c>FACTORYOS_SECRET_</c>.</param>
    public EnvironmentVariableSecretProvider(string prefix = "FACTORYOS_SECRET_")
    {
        _prefix = prefix ?? string.Empty;
    }

    /// <inheritdoc />
    public bool TryGet(string name, out string? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        value = Environment.GetEnvironmentVariable(_prefix + name);
        return value is not null;
    }
}
