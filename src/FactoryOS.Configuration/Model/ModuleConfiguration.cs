namespace FactoryOS.Configuration.Model;

/// <summary>The per-tenant configuration of an activated business module.</summary>
public sealed record ModuleConfiguration : IComponentConfiguration
{
    /// <summary>Gets the module key.</summary>
    public required string Key { get; init; }

    /// <summary>Gets a value indicating whether the module is enabled for the tenant.</summary>
    public bool Enabled { get; init; } = true;

    /// <summary>Gets the module settings.</summary>
    public IReadOnlyDictionary<string, string> Settings { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}
