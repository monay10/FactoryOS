using FactoryOS.Connectors.Framework.Security;
using Microsoft.Extensions.Configuration;

namespace FactoryOS.Connectors.Framework.Configuration;

/// <summary>
/// A connector's own configuration: whether it is enabled and the settings declared under its section.
/// Settings marked as secrets (prefixed with <see cref="ConnectorConstants.SecretPrefix"/>) are decrypted
/// on demand through <see cref="GetSecret"/> — plaintext values are never stored decrypted at rest.
/// </summary>
public sealed class ConnectorConfiguration
{
    private readonly IConnectorSecretProtector _protector;

    /// <summary>Initializes a new instance of the <see cref="ConnectorConfiguration"/> class.</summary>
    /// <param name="key">The connector key this configuration belongs to.</param>
    /// <param name="enabled">Whether the connector is enabled.</param>
    /// <param name="values">The connector's settings (secret values remain encrypted).</param>
    /// <param name="protector">The secret protector used to decrypt secret settings.</param>
    public ConnectorConfiguration(
        string key, bool enabled, IReadOnlyDictionary<string, string?> values, IConnectorSecretProtector protector)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(values);
        ArgumentNullException.ThrowIfNull(protector);

        Key = key;
        Enabled = enabled;
        Values = values;
        _protector = protector;
    }

    /// <summary>Gets the connector key.</summary>
    public string Key { get; }

    /// <summary>Gets a value indicating whether the connector is enabled.</summary>
    public bool Enabled { get; }

    /// <summary>Gets the connector's settings (secret values remain in their encrypted form).</summary>
    public IReadOnlyDictionary<string, string?> Values { get; }

    /// <summary>Reads a setting value as stored (secrets stay encrypted).</summary>
    /// <param name="name">The setting name.</param>
    /// <returns>The value, or <see langword="null"/> when absent.</returns>
    public string? Get(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return Values.TryGetValue(name, out var value) ? value : null;
    }

    /// <summary>Reads a setting value, decrypting it when it is an encrypted secret.</summary>
    /// <param name="name">The setting name.</param>
    /// <returns>The decrypted value, or <see langword="null"/> when absent.</returns>
    public string? GetSecret(string name)
    {
        var value = Get(name);
        return value is not null && _protector.IsProtected(value) ? _protector.Unprotect(value) : value;
    }

    /// <summary>Determines whether a setting is present.</summary>
    /// <param name="name">The setting name.</param>
    /// <returns><see langword="true"/> when the setting exists.</returns>
    public bool Has(string name) => Values.ContainsKey(name);
}

/// <summary>Supplies each connector's <see cref="ConnectorConfiguration"/> from the host configuration.</summary>
public interface IConnectorConfigurationProvider
{
    /// <summary>Reads the configuration for a connector from its section under <see cref="ConnectorConstants.ConnectorConfigurationSection"/>.</summary>
    /// <param name="key">The connector key.</param>
    /// <returns>The connector configuration (enabled by default, with no settings, when no section exists).</returns>
    ConnectorConfiguration GetConfiguration(string key);
}

/// <summary>
/// Default <see cref="IConnectorConfigurationProvider"/> reading <c>Connectors:Configuration:&lt;key&gt;</c>.
/// The <c>Enabled</c> child toggles the connector (default <see langword="true"/>); every other child is a setting.
/// </summary>
public sealed class ConnectorConfigurationProvider : IConnectorConfigurationProvider
{
    private readonly IConfiguration _configuration;
    private readonly IConnectorSecretProtector _protector;

    /// <summary>Initializes a new instance of the <see cref="ConnectorConfigurationProvider"/> class.</summary>
    /// <param name="configuration">The host configuration.</param>
    /// <param name="protector">The secret protector used by the produced configurations.</param>
    public ConnectorConfigurationProvider(IConfiguration configuration, IConnectorSecretProtector protector)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(protector);
        _configuration = configuration;
        _protector = protector;
    }

    /// <inheritdoc />
    public ConnectorConfiguration GetConfiguration(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var section = _configuration.GetSection($"{ConnectorConstants.ConnectorConfigurationSection}:{key}");

        var enabled = true;
        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var child in section.GetChildren())
        {
            if (string.Equals(child.Key, ConnectorConstants.EnabledKey, StringComparison.OrdinalIgnoreCase))
            {
                enabled = !bool.TryParse(child.Value, out var parsed) || parsed;
                continue;
            }

            values[child.Key] = child.Value;
        }

        return new ConnectorConfiguration(key, enabled, values, _protector);
    }
}
