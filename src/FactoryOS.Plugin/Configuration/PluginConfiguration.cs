using Microsoft.Extensions.Configuration;

namespace FactoryOS.Plugin.Configuration;

/// <summary>
/// A plugin's own configuration: whether it is enabled and the string settings declared under its
/// configuration section. Plugin configuration is <b>data</b> — the core reads it without knowing the
/// plugin by name.
/// </summary>
public sealed class PluginConfiguration
{
    /// <summary>Initializes a new instance of the <see cref="PluginConfiguration"/> class.</summary>
    /// <param name="key">The plugin key this configuration belongs to.</param>
    /// <param name="enabled">Whether the plugin is enabled.</param>
    /// <param name="values">The plugin's string settings.</param>
    public PluginConfiguration(string key, bool enabled, IReadOnlyDictionary<string, string?> values)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(values);

        Key = key;
        Enabled = enabled;
        Values = values;
    }

    /// <summary>Gets the plugin key.</summary>
    public string Key { get; }

    /// <summary>Gets a value indicating whether the plugin is enabled.</summary>
    public bool Enabled { get; }

    /// <summary>Gets the plugin's string settings.</summary>
    public IReadOnlyDictionary<string, string?> Values { get; }

    /// <summary>Reads a setting value.</summary>
    /// <param name="name">The setting name.</param>
    /// <returns>The value, or <see langword="null"/> when absent.</returns>
    public string? Get(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return Values.TryGetValue(name, out var value) ? value : null;
    }

    /// <summary>Determines whether a setting is present.</summary>
    /// <param name="name">The setting name.</param>
    /// <returns><see langword="true"/> when the setting exists.</returns>
    public bool Has(string name) => Values.ContainsKey(name);
}

/// <summary>Supplies each plugin's <see cref="PluginConfiguration"/> from the host configuration.</summary>
public interface IPluginConfigurationProvider
{
    /// <summary>Reads the configuration for a plugin from its section under <see cref="PluginConstants.PluginConfigurationSection"/>.</summary>
    /// <param name="key">The plugin key.</param>
    /// <returns>The plugin configuration (enabled by default, with no settings, when no section exists).</returns>
    PluginConfiguration GetConfiguration(string key);
}

/// <summary>
/// Default <see cref="IPluginConfigurationProvider"/> reading <c>Plugins:Configuration:&lt;key&gt;</c>. The
/// <c>Enabled</c> child toggles the plugin (default <see langword="true"/>); every other child is a setting.
/// </summary>
public sealed class PluginConfigurationProvider : IPluginConfigurationProvider
{
    private readonly IConfiguration _configuration;

    /// <summary>Initializes a new instance of the <see cref="PluginConfigurationProvider"/> class.</summary>
    /// <param name="configuration">The host configuration.</param>
    public PluginConfigurationProvider(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        _configuration = configuration;
    }

    /// <inheritdoc />
    public PluginConfiguration GetConfiguration(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var section = _configuration.GetSection($"{PluginConstants.PluginConfigurationSection}:{key}");

        var enabled = true;
        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var child in section.GetChildren())
        {
            if (string.Equals(child.Key, PluginConstants.EnabledKey, StringComparison.OrdinalIgnoreCase))
            {
                enabled = !bool.TryParse(child.Value, out var parsed) || parsed;
                continue;
            }

            values[child.Key] = child.Value;
        }

        return new PluginConfiguration(key, enabled, values);
    }
}
