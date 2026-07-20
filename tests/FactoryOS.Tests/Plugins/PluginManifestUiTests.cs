using FactoryOS.Plugin.Manifest;

namespace FactoryOS.Tests.Plugins;

public sealed class PluginManifestUiTests
{
    [Fact]
    public void Reads_ui_screens_from_the_manifest()
    {
        const string Json = """
        {
          "key": "energy",
          "name": "Energy",
          "version": "1.2.0",
          "ui": [
            {
              "id": "energy.overview",
              "title": "Overview",
              "route": "/energy/overview",
              "component": "energy/Overview",
              "icon": "bolt",
              "requiredPermission": "energy.read",
              "navSection": "Energy",
              "order": 5
            }
          ]
        }
        """;

        var result = PluginManifestReader.Read(Json);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Description : null);
        var screen = Assert.Single(result.Value.Ui);
        Assert.Equal("energy.overview", screen.Id);
        Assert.Equal("Overview", screen.Title);
        Assert.Equal("/energy/overview", screen.Route);
        Assert.Equal("energy/Overview", screen.Component);
        Assert.Equal("bolt", screen.Icon);
        Assert.Equal("energy.read", screen.RequiredPermission);
        Assert.Equal("Energy", screen.NavSection);
        Assert.Equal(5, screen.Order);
    }

    [Fact]
    public void Defaults_ui_to_empty_when_absent()
    {
        const string Json = """
        { "key": "quality", "name": "Quality", "version": "1.0.0" }
        """;

        var result = PluginManifestReader.Read(Json);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value.Ui);
    }

    [Fact]
    public void Rejects_a_ui_screen_missing_required_fields()
    {
        const string Json = """
        {
          "key": "quality",
          "name": "Quality",
          "version": "1.0.0",
          "ui": [ { "id": "q.home", "title": "Home", "route": "/quality" } ]
        }
        """;

        var result = PluginManifestReader.Read(Json);

        Assert.True(result.IsFailure);
        Assert.Equal("Plugin.Manifest.InvalidUiScreen", result.Error.Code);
    }
}
