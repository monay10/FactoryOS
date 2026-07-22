using System.Globalization;

namespace FactoryOS.Plugins.Runtime.Domain;

/// <summary>
/// One tenant's settings for one plugin — the configuration-isolation unit.
/// <para>
/// Settings are held per <c>tenant + plugin</c> and never merged across tenants. Two factories running the
/// same plugin at the same version have two independent setting sets, so there is no shared mutable
/// configuration one factory could change under another.
/// </para>
/// </summary>
public sealed class PluginSettings
{
    private readonly Dictionary<string, string?> _values;

    /// <summary>Initializes a new instance of the <see cref="PluginSettings"/> class.</summary>
    /// <param name="tenant">The tenant the settings belong to.</param>
    /// <param name="pluginKey">The plugin the settings configure.</param>
    /// <param name="values">The setting values.</param>
    public PluginSettings(string tenant, string pluginKey, IReadOnlyDictionary<string, string?>? values = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginKey);

        Tenant = tenant;
        PluginKey = pluginKey;
        _values = values is null
            ? new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string?>(values, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Gets the tenant the settings belong to.</summary>
    public string Tenant { get; }

    /// <summary>Gets the plugin the settings configure.</summary>
    public string PluginKey { get; }

    /// <summary>Gets the setting values.</summary>
    public IReadOnlyDictionary<string, string?> Values => _values;

    /// <summary>Gets the number of settings held.</summary>
    public int Count => _values.Count;

    /// <summary>Gets the configuration section these settings are surfaced under.</summary>
    public string Section =>
        string.Create(CultureInfo.InvariantCulture, $"Plugins:Tenants:{Tenant}:{PluginKey}");

    /// <summary>Reads a setting.</summary>
    /// <param name="name">The setting name.</param>
    /// <returns>The value, or <see langword="null"/> when absent.</returns>
    public string? Get(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return _values.TryGetValue(name, out var value) ? value : null;
    }

    /// <summary>Determines whether a setting is present.</summary>
    /// <param name="name">The setting name.</param>
    /// <returns><see langword="true"/> when the setting exists.</returns>
    public bool Has(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return _values.ContainsKey(name);
    }

    /// <summary>Reads a setting as an integer.</summary>
    /// <param name="name">The setting name.</param>
    /// <param name="fallback">The value returned when the setting is absent or not a number.</param>
    /// <returns>The parsed value, or <paramref name="fallback"/>.</returns>
    public int GetInt(string name, int fallback = 0) =>
        int.TryParse(Get(name), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : fallback;

    /// <summary>Reads a setting as a boolean.</summary>
    /// <param name="name">The setting name.</param>
    /// <param name="fallback">The value returned when the setting is absent or not a boolean.</param>
    /// <returns>The parsed value, or <paramref name="fallback"/>.</returns>
    public bool GetBool(string name, bool fallback = false) =>
        bool.TryParse(Get(name), out var value) ? value : fallback;

    /// <summary>Sets or replaces a single setting.</summary>
    /// <param name="name">The setting name.</param>
    /// <param name="value">The value.</param>
    public void Set(string name, string? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        _values[name] = value;
    }

    /// <summary>Replaces every setting at once.</summary>
    /// <param name="values">The new settings.</param>
    public void Replace(IReadOnlyDictionary<string, string?> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        _values.Clear();
        foreach (var pair in values)
        {
            _values[pair.Key] = pair.Value;
        }
    }
}

/// <summary>
/// What one plugin instance is currently consuming, against what it is allowed.
/// <para>
/// Usage is tracked per instance rather than per plugin, so exceeding a quota degrades one factory and not
/// every factory that happens to run the same plugin.
/// </para>
/// </summary>
/// <param name="Kind">The resource.</param>
/// <param name="Used">How much is in use.</param>
/// <param name="Limit">The limit, or <c>0</c> when unlimited.</param>
public sealed record PluginResourceUsage(PluginResourceKind Kind, long Used, long Limit)
{
    /// <summary>Gets a value indicating whether a limit is enforced for this resource.</summary>
    public bool IsLimited => Limit > 0;

    /// <summary>Gets a value indicating whether the usage is over its limit.</summary>
    public bool IsExceeded => IsLimited && Used > Limit;

    /// <summary>Gets how much headroom remains, or <see cref="long.MaxValue"/> when unlimited.</summary>
    public long Remaining => IsLimited ? Math.Max(0, Limit - Used) : long.MaxValue;

    /// <inheritdoc />
    public override string ToString() => IsLimited
        ? string.Create(CultureInfo.InvariantCulture, $"{Kind} {Used}/{Limit}")
        : string.Create(CultureInfo.InvariantCulture, $"{Kind} {Used}/unlimited");
}
