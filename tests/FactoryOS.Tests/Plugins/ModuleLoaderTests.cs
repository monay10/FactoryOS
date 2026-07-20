using FactoryOS.Contracts.Plugins;
using FactoryOS.Plugin.Loading;
using FactoryOS.Plugin.Runtime;
using FactoryOS.Plugins.Sample;

namespace FactoryOS.Tests.Plugins;

public sealed class ModuleLoaderTests
{
    private static string SampleDirectory =>
        Path.GetDirectoryName(typeof(SamplePlugin).Assembly.Location)!;

    private static PluginManifest SampleManifest(
        string key = "sample",
        string? assembly = "FactoryOS.Plugins.Sample.dll",
        string? entryType = "FactoryOS.Plugins.Sample.SamplePlugin") => new()
    {
        Key = key,
        Name = "Sample Plugin",
        Version = new PluginVersion(1, 0, 0),
        Assembly = assembly,
        EntryType = entryType,
    };

    [Fact]
    public void Loads_and_activates_the_plugin_from_its_manifest()
    {
        var loader = new ModuleLoader();
        var descriptor = new PluginDescriptor(SampleManifest(), SampleDirectory);

        var result = loader.Load(descriptor);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Description : null);
        Assert.Equal(SamplePlugin.PluginKey, result.Value.Key);
    }

    [Fact]
    public void Loads_the_single_plugin_when_no_entry_type_is_declared()
    {
        var loader = new ModuleLoader();
        var descriptor = new PluginDescriptor(SampleManifest(entryType: null), SampleDirectory);

        var result = loader.Load(descriptor);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Description : null);
        Assert.Equal(SamplePlugin.PluginKey, result.Value.Key);
    }

    [Fact]
    public void Loaded_plugin_shares_the_host_contract_types()
    {
        var loader = new ModuleLoader();
        var descriptor = new PluginDescriptor(SampleManifest(), SampleDirectory);

        var plugin = loader.Load(descriptor).Value;

        // The instance loaded in the isolated context is still assignable to the host's IPlugin,
        // proving the shared contract assembly was unified with the default context.
        Assert.IsAssignableFrom<IPlugin>(plugin);
    }

    [Fact]
    public void Fails_when_the_manifest_key_does_not_match_the_loaded_plugin()
    {
        var loader = new ModuleLoader();
        var descriptor = new PluginDescriptor(SampleManifest(key: "not-sample"), SampleDirectory);

        var result = loader.Load(descriptor);

        Assert.True(result.IsFailure);
        Assert.Equal("Plugin.Load.KeyMismatch", result.Error.Code);
    }

    [Fact]
    public void Fails_when_the_entry_assembly_is_missing()
    {
        var loader = new ModuleLoader();
        var descriptor = new PluginDescriptor(SampleManifest(assembly: "Does.Not.Exist.dll"), SampleDirectory);

        var result = loader.Load(descriptor);

        Assert.True(result.IsFailure);
        Assert.Equal("Plugin.Load.Failed", result.Error.Code);
    }

    [Fact]
    public void Fails_when_the_declared_entry_type_is_absent()
    {
        var loader = new ModuleLoader();
        var descriptor = new PluginDescriptor(
            SampleManifest(entryType: "FactoryOS.Plugins.Sample.Nonexistent"),
            SampleDirectory);

        var result = loader.Load(descriptor);

        Assert.True(result.IsFailure);
        Assert.Equal("Plugin.Load.EntryTypeNotFound", result.Error.Code);
    }
}
