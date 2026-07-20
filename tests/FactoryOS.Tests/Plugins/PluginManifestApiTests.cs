using FactoryOS.Plugin.Manifest;

namespace FactoryOS.Tests.Plugins;

public sealed class PluginManifestApiTests
{
    [Fact]
    public void Reads_api_routes_from_the_manifest()
    {
        const string Json = """
        {
          "key": "deliveryhealth",
          "name": "Delivery Health",
          "version": "1.0.0",
          "api": [
            {
              "method": "GET",
              "path": "/m/deliveryhealth/health",
              "query": [ "tenant" ],
              "description": "Per-transport delivery tallies for a tenant."
            }
          ]
        }
        """;

        var result = PluginManifestReader.Read(Json);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Description : null);
        var route = Assert.Single(result.Value.Api);
        Assert.Equal("GET", route.Method);
        Assert.Equal("/m/deliveryhealth/health", route.Path);
        Assert.Equal("tenant", Assert.Single(route.Query));
        Assert.Equal("Per-transport delivery tallies for a tenant.", route.Description);
    }

    [Fact]
    public void Defaults_api_to_empty_when_absent()
    {
        const string Json = """
        { "key": "quality", "name": "Quality", "version": "1.0.0" }
        """;

        var result = PluginManifestReader.Read(Json);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value.Api);
    }

    [Fact]
    public void Rejects_an_api_route_missing_method_or_path()
    {
        const string Json = """
        {
          "key": "quality",
          "name": "Quality",
          "version": "1.0.0",
          "api": [ { "path": "/m/quality/x" } ]
        }
        """;

        var result = PluginManifestReader.Read(Json);

        Assert.True(result.IsFailure);
        Assert.Equal("Plugin.Manifest.InvalidApiRoute", result.Error.Code);
    }
}
