using FactoryOS.Contracts.Plugins;
using FactoryOS.Plugin.Discovery;
using FactoryOS.Plugin.Runtime;

namespace FactoryOS.Tests.Plugins;

/// <summary>
/// A repo-wide conformance guard over the <b>real</b> plugin manifests on disk. A module's HTTP read routes are
/// discoverable as data only if the manifest declares them, so the gateway's <c>/modules/api</c> catalog advertises
/// exactly what the module serves. This test reads every <c>plugins/*/module.json</c> and asserts every declared
/// route is well-formed and namespaced under the module's own <c>/m/&lt;key&gt;</c> prefix — and that the modules
/// which contribute a read API actually declare it (the drift a hand-edited manifest silently introduces).
/// </summary>
public sealed class PluginManifestApiConformanceTests
{
    private static readonly string PluginsRoot = LocatePluginsRoot();

    private static IReadOnlyList<PluginDescriptor> RealPlugins() => new PluginDiscovery().Discover(PluginsRoot);

    [Fact]
    public void Every_real_manifest_parses()
    {
        var descriptors = RealPlugins();

        Assert.NotEmpty(descriptors);
        Assert.All(descriptors, descriptor =>
            Assert.False(descriptor.State == PluginState.Failed, $"{descriptor.Location}: {descriptor.FailureReason}"));
    }

    // Read routes are GET; a write route (e.g. posing a question to the Brain) is POST. Both are gateway-mounted
    // under the module's own prefix — no other verbs are declared, so the /modules/api catalog stays predictable.
    private static readonly HashSet<string> SupportedMethods = new(StringComparer.Ordinal) { "GET", "POST" };

    [Fact]
    public void Every_declared_route_uses_a_supported_method_and_is_namespaced_under_its_module()
    {
        foreach (var descriptor in RealPlugins())
        {
            var prefix = $"/m/{descriptor.Key}/";
            foreach (var route in descriptor.Manifest.Api)
            {
                Assert.Contains(route.Method, SupportedMethods);
                Assert.StartsWith(prefix, route.Path, StringComparison.Ordinal);
            }
        }
    }

    [Theory]
    [InlineData("oee", "/m/oee/snapshots", "/m/oee/summary")]
    [InlineData("maintenance", "/m/maintenance/workorders", "/m/maintenance/summary")]
    [InlineData("warehouse", "/m/warehouse/stock", "/m/warehouse/summary")]
    [InlineData("quality", "/m/quality/lines", "/m/quality/summary")]
    [InlineData("dashboard", "/m/dashboard/board", "/m/dashboard/summary")]
    public void Read_model_modules_declare_their_routes(string key, params string[] expectedPaths)
    {
        var descriptor = Assert.Single(RealPlugins(), d => d.Key == key);

        var declared = descriptor.Manifest.Api.Select(route => route.Path).ToHashSet(StringComparer.Ordinal);

        foreach (var path in expectedPaths)
        {
            Assert.Contains(path, declared);
        }
    }

    private static string LocatePluginsRoot()
    {
        // Walk up from the test binary until the repository root — the directory holding both the solution and plugins.
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "FactoryOS.slnx")) &&
                Directory.Exists(Path.Combine(directory.FullName, "plugins")))
            {
                return Path.Combine(directory.FullName, "plugins");
            }
        }

        throw new InvalidOperationException("Could not locate the repository 'plugins' directory from " + AppContext.BaseDirectory);
    }
}
