using FactoryOS.Contracts.Plugins;
using FactoryOS.Plugin.Manifest;

namespace FactoryOS.Tests.Plugins;

public sealed class PluginManifestReaderTests
{
    [Fact]
    public void Reads_a_complete_manifest()
    {
        const string json = """
        {
          "key": "energy",
          "name": "Energy Module",
          "version": "1.4.2",
          "description": "Energy monitoring.",
          "author": "FactoryOS",
          "assembly": "FactoryOS.Plugins.Energy.dll",
          "entryType": "FactoryOS.Plugins.Energy.EnergyPlugin",
          "dependencies": [ { "key": "core", "minimumVersion": "1.0.0" } ],
          "provides": [ "energy.dashboard" ],
          "consumes": [ "MeterReading" ],
          "emits": [ "EnergyThresholdExceeded" ]
        }
        """;

        var result = PluginManifestReader.Read(json);

        Assert.True(result.IsSuccess);
        var manifest = result.Value;
        Assert.Equal("energy", manifest.Key);
        Assert.Equal(PluginVersion.Parse("1.4.2"), manifest.Version);
        var dependency = Assert.Single(manifest.Dependencies);
        Assert.Equal("core", dependency.PluginKey);
        Assert.Equal(PluginVersion.Parse("1.0.0"), dependency.MinimumVersion);
        Assert.Equal("energy.dashboard", Assert.Single(manifest.Provides));
        Assert.Equal("MeterReading", Assert.Single(manifest.Consumes));
        Assert.Equal("EnergyThresholdExceeded", Assert.Single(manifest.Emits));
    }

    [Fact]
    public void Empty_json_fails()
    {
        var result = PluginManifestReader.Read("   ");

        Assert.True(result.IsFailure);
        Assert.Equal("Plugin.Manifest.Empty", result.Error.Code);
    }

    [Fact]
    public void Malformed_json_fails()
    {
        var result = PluginManifestReader.Read("{ not json");

        Assert.True(result.IsFailure);
        Assert.Equal("Plugin.Manifest.Malformed", result.Error.Code);
    }

    [Fact]
    public void Missing_key_fails()
    {
        var result = PluginManifestReader.Read("""{ "name": "X", "version": "1.0.0" }""");

        Assert.True(result.IsFailure);
        Assert.Equal("Plugin.Manifest.MissingKey", result.Error.Code);
    }

    [Fact]
    public void Invalid_version_fails()
    {
        var result = PluginManifestReader.Read("""{ "key": "x", "name": "X", "version": "1.0" }""");

        Assert.True(result.IsFailure);
        Assert.Equal("Plugin.Manifest.InvalidVersion", result.Error.Code);
    }

    [Fact]
    public void Dependency_with_invalid_version_fails()
    {
        const string json = """
        { "key": "x", "name": "X", "version": "1.0.0",
          "dependencies": [ { "key": "core", "minimumVersion": "bad" } ] }
        """;

        var result = PluginManifestReader.Read(json);

        Assert.True(result.IsFailure);
        Assert.Equal("Plugin.Manifest.InvalidDependency", result.Error.Code);
    }
}
