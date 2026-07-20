using FactoryOS.Contracts.Plugins;
using FactoryOS.Plugin.Hosting;
using FactoryOS.Plugin.Registry;
using FactoryOS.Plugin.Runtime;
using FactoryOS.Plugins.Sample;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace FactoryOS.Tests.Plugins;

public sealed class PluginHostTests
{
    private sealed class RecordingPlugin : PluginBase
    {
        private readonly List<string> _log;
        private readonly string _key;

        public RecordingPlugin(string key, List<string> log)
        {
            _key = key;
            _log = log;
        }

        public override string Key => _key;

        public override void ConfigureServices(IServiceCollection services) => _log.Add($"configure:{_key}");

        public override Task StartAsync(CancellationToken cancellationToken)
        {
            _log.Add($"start:{_key}");
            return Task.CompletedTask;
        }

        public override Task StopAsync(CancellationToken cancellationToken)
        {
            _log.Add($"stop:{_key}");
            return Task.CompletedTask;
        }
    }

    private static PluginDescriptor Descriptor(string key, params PluginDependency[] dependencies)
    {
        return new PluginDescriptor(new PluginManifest
        {
            Key = key,
            Name = key,
            Version = PluginVersion.Parse("1.0.0"),
            Dependencies = dependencies,
        });
    }

    private static PluginHost BuildHost(IPluginRegistry registry, IEnumerable<IPlugin> plugins)
    {
        return new PluginHost(registry, plugins, NullLogger<PluginHost>.Instance);
    }

    [Fact]
    public async Task Configure_and_start_follow_dependency_order()
    {
        var log = new List<string>();
        var registry = new PluginRegistry();
        registry.Register(Descriptor("b", new PluginDependency("a", PluginVersion.Parse("1.0.0"))));
        registry.Register(Descriptor("a"));

        var host = BuildHost(registry, [new RecordingPlugin("b", log), new RecordingPlugin("a", log)]);

        host.ConfigureServices(new ServiceCollection());
        await host.StartAsync(CancellationToken.None);

        Assert.Equal(["configure:a", "configure:b", "start:a", "start:b"], log);
    }

    [Fact]
    public async Task Stop_runs_in_reverse_order()
    {
        var log = new List<string>();
        var registry = new PluginRegistry();
        registry.Register(Descriptor("b", new PluginDependency("a", PluginVersion.Parse("1.0.0"))));
        registry.Register(Descriptor("a"));

        var host = BuildHost(registry, [new RecordingPlugin("a", log), new RecordingPlugin("b", log)]);
        host.ConfigureServices(new ServiceCollection());
        log.Clear();

        await host.StopAsync(CancellationToken.None);

        Assert.Equal(["stop:b", "stop:a"], log);
    }

    [Fact]
    public void Disabled_plugins_are_skipped()
    {
        var log = new List<string>();
        var registry = new PluginRegistry();
        registry.Register(Descriptor("a"));
        registry.Register(Descriptor("b"));
        registry.Disable("b");

        var host = BuildHost(registry, [new RecordingPlugin("a", log), new RecordingPlugin("b", log)]);
        host.ConfigureServices(new ServiceCollection());

        Assert.Equal("a", Assert.Single(host.LoadOrder).Key);
        Assert.DoesNotContain("configure:b", log);
    }

    [Fact]
    public void Descriptor_without_instance_is_marked_failed()
    {
        var registry = new PluginRegistry();
        registry.Register(Descriptor("orphan"));

        var host = BuildHost(registry, []);
        host.ConfigureServices(new ServiceCollection());

        Assert.Empty(host.LoadOrder);
        Assert.Equal(PluginState.Failed, registry.Find("orphan")!.State);
    }

    [Fact]
    public async Task Sample_plugin_contributes_its_services_and_starts()
    {
        var registry = new PluginRegistry();
        registry.Register(new PluginDescriptor(new PluginManifest
        {
            Key = SamplePlugin.PluginKey,
            Name = "Sample Plugin",
            Version = PluginVersion.Parse("1.0.0"),
        }));

        var host = BuildHost(registry, [new SamplePlugin()]);
        var services = new ServiceCollection();

        host.ConfigureServices(services);
        await host.StartAsync(CancellationToken.None);

        using var provider = services.BuildServiceProvider();
        var greeter = provider.GetRequiredService<ISampleGreeter>();
        Assert.Contains("sample plugin", greeter.Greet("Factory"), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(PluginState.Started, registry.Find(SamplePlugin.PluginKey)!.State);
    }
}
