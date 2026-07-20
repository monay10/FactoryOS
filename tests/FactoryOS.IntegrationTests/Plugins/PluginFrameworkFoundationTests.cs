using FactoryOS.Contracts.Plugins;
using FactoryOS.Domain.Abstractions;
using FactoryOS.Domain.Time;
using FactoryOS.Plugin.Catalog;
using FactoryOS.Plugin.Configuration;
using FactoryOS.Plugin.Health;
using FactoryOS.Plugin.Management;
using FactoryOS.Plugin.Registry;
using FactoryOS.Plugin.Runtime;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FactoryOS.IntegrationTests.Plugins;

/// <summary>
/// The plugin framework foundation composed through <c>AddPluginFramework(configuration)</c> against a real
/// container: several plugins register, start and stop through the manager, the catalog projects their
/// metadata and health, a plugin reloads in place, and per-plugin configuration is read from the host.
/// </summary>
public sealed class PluginFrameworkFoundationTests
{
    private sealed class CountingPlugin : PluginBase
    {
        private readonly string _key;

        public CountingPlugin(string key) => _key = key;

        public int Starts { get; private set; }

        public int Stops { get; private set; }

        public override string Key => _key;

        public override Task StartAsync(CancellationToken cancellationToken)
        {
            Starts++;
            return Task.CompletedTask;
        }

        public override Task StopAsync(CancellationToken cancellationToken)
        {
            Stops++;
            return Task.CompletedTask;
        }
    }

    private static ServiceProvider BuildProvider()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Plugins:Health:HeartbeatIntervalSeconds"] = "30",
                ["Plugins:Configuration:energy:Region"] = "eu-west",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();
        services.AddPluginFramework(configuration);
        return services.BuildServiceProvider(validateScopes: true);
    }

    private static PluginDescriptor Register(IPluginRegistry registry, IPlugin plugin)
    {
        var descriptor = new PluginDescriptor(new PluginManifest
        {
            Key = plugin.Key,
            Name = plugin.Key,
            Version = PluginVersion.Parse("1.0.0"),
            Provides = [$"{plugin.Key}.feature"],
        });
        descriptor.AttachInstance(plugin);
        registry.Register(descriptor);
        return descriptor;
    }

    [Fact]
    public async Task Multiple_plugins_start_reload_and_stop_through_the_manager()
    {
        using var provider = BuildProvider();

        var registry = provider.GetRequiredService<IPluginRegistry>();
        var energy = new CountingPlugin("energy");
        var oee = new CountingPlugin("oee");
        Register(registry, energy);
        Register(registry, oee);

        var manager = provider.GetRequiredService<IPluginManager>();
        var catalog = provider.GetRequiredService<IPluginCatalog>();

        Assert.True((await manager.StartAsync("energy")).IsSuccess);
        Assert.True((await manager.StartAsync("oee")).IsSuccess);

        // Both plugins are catalogued and healthy after their first heartbeat.
        Assert.Equal(2, catalog.List().Count);
        Assert.Single(catalog.WithCapability("energy.feature"));
        Assert.Equal(2, catalog.Health().Count(h => h.Status == PluginHealthStatus.Healthy));

        // Reload energy in place: one extra stop + start.
        Assert.True((await manager.ReloadAsync("energy")).IsSuccess);
        Assert.Equal(2, energy.Starts);
        Assert.Equal(1, energy.Stops);

        // Shut both down.
        Assert.True((await manager.StopAsync("energy")).IsSuccess);
        Assert.True((await manager.StopAsync("oee")).IsSuccess);
        Assert.Equal(PluginState.Loaded, registry.Find("energy")!.State);
        Assert.Equal(1, oee.Stops);
    }

    [Fact]
    public void Per_plugin_configuration_is_read_from_the_host()
    {
        using var provider = BuildProvider();

        var configuration = provider.GetRequiredService<IPluginConfigurationProvider>().GetConfiguration("energy");

        Assert.True(configuration.Enabled);
        Assert.Equal("eu-west", configuration.Get("Region"));
    }
}
