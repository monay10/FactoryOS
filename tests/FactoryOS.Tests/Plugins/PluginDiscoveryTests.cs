using FactoryOS.Contracts.Plugins;
using FactoryOS.Plugin.Discovery;

namespace FactoryOS.Tests.Plugins;

public sealed class PluginDiscoveryTests : IDisposable
{
    private readonly string _root;

    public PluginDiscoveryTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "factoryos-discovery-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    [Fact]
    public void Discovers_valid_plugin_folders()
    {
        WritePlugin("energy", """{ "key": "energy", "name": "Energy", "version": "1.0.0" }""");
        WritePlugin("quality", """{ "key": "quality", "name": "Quality", "version": "2.1.0" }""");

        var descriptors = new PluginDiscovery().Discover(_root);

        Assert.Equal(2, descriptors.Count);
        Assert.Contains(descriptors, d => d.Key == "energy" && d.State == PluginState.Discovered);
        Assert.All(descriptors, d => Assert.NotNull(d.Location));
    }

    [Fact]
    public void Folder_without_manifest_is_ignored()
    {
        Directory.CreateDirectory(Path.Combine(_root, "empty"));

        var descriptors = new PluginDiscovery().Discover(_root);

        Assert.Empty(descriptors);
    }

    [Fact]
    public void Invalid_manifest_yields_a_failed_descriptor()
    {
        WritePlugin("broken", """{ "name": "Broken" }""");

        var descriptor = Assert.Single(new PluginDiscovery().Discover(_root));

        Assert.Equal(PluginState.Failed, descriptor.State);
        Assert.NotNull(descriptor.FailureReason);
    }

    [Fact]
    public void Missing_root_returns_empty()
    {
        var descriptors = new PluginDiscovery().Discover(Path.Combine(_root, "does-not-exist"));

        Assert.Empty(descriptors);
    }

    private void WritePlugin(string folder, string manifestJson)
    {
        var directory = Path.Combine(_root, folder);
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, PluginDiscovery.ManifestFileName), manifestJson);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
