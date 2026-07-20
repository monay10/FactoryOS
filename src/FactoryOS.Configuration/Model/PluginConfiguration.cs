namespace FactoryOS.Configuration.Model;

/// <summary>The per-tenant configuration of an activated plugin (connector, agent, dashboard, …).</summary>
public sealed record PluginConfiguration : IComponentConfiguration
{
    /// <summary>Gets the plugin key.</summary>
    public required string Key { get; init; }

    /// <summary>Gets a value indicating whether the plugin is enabled for the tenant.</summary>
    public bool Enabled { get; init; } = true;

    /// <summary>Gets the plugin settings.</summary>
    public IReadOnlyDictionary<string, string> Settings { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}
