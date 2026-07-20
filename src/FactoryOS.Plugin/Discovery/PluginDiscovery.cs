using FactoryOS.Plugin.Manifest;
using FactoryOS.Plugin.Runtime;

namespace FactoryOS.Plugin.Discovery;

/// <summary>
/// Default <see cref="IPluginDiscovery"/> that treats each immediate subdirectory of the root as a
/// plugin folder containing a <c>module.json</c> manifest.
/// </summary>
public sealed class PluginDiscovery : IPluginDiscovery
{
    /// <summary>The conventional manifest file name every plugin folder must contain.</summary>
    public const string ManifestFileName = "module.json";

    /// <inheritdoc />
    public IReadOnlyList<PluginDescriptor> Discover(string rootDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);

        if (!Directory.Exists(rootDirectory))
        {
            return [];
        }

        var descriptors = new List<PluginDescriptor>();

        foreach (var directory in Directory.EnumerateDirectories(rootDirectory).OrderBy(path => path, StringComparer.Ordinal))
        {
            var manifestPath = Path.Combine(directory, ManifestFileName);
            if (!File.Exists(manifestPath))
            {
                continue;
            }

            var result = PluginManifestReader.ReadFile(manifestPath);
            if (result.IsSuccess)
            {
                descriptors.Add(new PluginDescriptor(result.Value, directory));
            }
            else
            {
                descriptors.Add(FailedDescriptor(directory, result.Error.Description));
            }
        }

        return descriptors;
    }

    private static PluginDescriptor FailedDescriptor(string directory, string reason)
    {
        var placeholder = new Contracts.Plugins.PluginManifest
        {
            Key = Path.GetFileName(directory),
            Name = Path.GetFileName(directory),
            Version = default,
        };

        var descriptor = new PluginDescriptor(placeholder, directory);
        descriptor.MarkFailed(reason);
        return descriptor;
    }
}
