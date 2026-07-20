namespace FactoryOS.Configuration.Model;

/// <summary>
/// Common shape of an activatable component's configuration (a module or a plugin): a key, an
/// enabled flag and a bag of strongly-addressable string settings.
/// </summary>
public interface IComponentConfiguration
{
    /// <summary>Gets the component key.</summary>
    string Key { get; }

    /// <summary>Gets a value indicating whether the component is activated for the tenant.</summary>
    bool Enabled { get; }

    /// <summary>Gets the component's settings.</summary>
    IReadOnlyDictionary<string, string> Settings { get; }
}
