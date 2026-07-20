namespace FactoryOS.Contracts.Plugins;

/// <summary>
/// A declared dependency of one plugin on another, expressed as the required plugin key and the
/// minimum acceptable version.
/// </summary>
/// <param name="PluginKey">The key of the plugin that must be present.</param>
/// <param name="MinimumVersion">The lowest version of the depended-upon plugin that satisfies this dependency.</param>
public sealed record PluginDependency(string PluginKey, PluginVersion MinimumVersion)
{
    /// <summary>Determines whether a candidate version satisfies this dependency.</summary>
    /// <param name="version">The version offered by the depended-upon plugin.</param>
    /// <returns><see langword="true"/> when <paramref name="version"/> meets or exceeds the minimum.</returns>
    public bool IsSatisfiedBy(PluginVersion version) => version >= MinimumVersion;
}
