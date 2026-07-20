using FactoryOS.Contracts.Plugins;
using FactoryOS.Domain.Abstractions;
using FactoryOS.Plugin.Activation;
using FactoryOS.Plugin.Catalog;
using FactoryOS.Plugin.Configuration;
using FactoryOS.Plugin.Health;
using FactoryOS.Plugin.Lifecycle;
using FactoryOS.Plugin.Management;
using FactoryOS.Plugin.Registry;
using FactoryOS.Plugin.Runtime;
using FactoryOS.Tests.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace FactoryOS.Tests.Plugins;

public sealed class PluginFrameworkFoundationTests
{
    private static readonly DateTimeOffset Now = new(2026, 07, 20, 12, 00, 00, TimeSpan.Zero);

    private static IOptions<PluginOptions> Options(Action<PluginOptions>? configure = null)
    {
        var options = new PluginOptions();
        configure?.Invoke(options);
        return Microsoft.Extensions.Options.Options.Create(options);
    }

    private sealed class LifecyclePlugin : PluginBase, IPluginLifecycle, IPluginHealthCheck
    {
        private readonly string _key;

        public LifecyclePlugin(string key) => _key = key;

        public List<string> Log { get; } = [];

        public override string Key => _key;

        public override Task StartAsync(CancellationToken cancellationToken)
        {
            Log.Add("start");
            return Task.CompletedTask;
        }

        public override Task StopAsync(CancellationToken cancellationToken)
        {
            Log.Add("stop");
            return Task.CompletedTask;
        }

        public Task InitializeAsync(IPluginContext context, CancellationToken cancellationToken)
        {
            Log.Add($"init:{context.Key}");
            return Task.CompletedTask;
        }

        public Task UnloadAsync(CancellationToken cancellationToken)
        {
            Log.Add("unload");
            return Task.CompletedTask;
        }

        public PluginHealthStatus Check() => PluginHealthStatus.Healthy;
    }

    private static PluginManifest Manifest(string key, params string[] provides) => new()
    {
        Key = key,
        Name = key,
        Version = PluginVersion.Parse("1.0.0"),
        Provides = provides,
    };

    private static PluginDescriptor Loaded(IPlugin instance)
    {
        var descriptor = new PluginDescriptor(Manifest(instance.Key));
        descriptor.AttachInstance(instance);
        return descriptor;
    }

    // ---- Configuration --------------------------------------------------------

    [Fact]
    public void Options_carry_the_documented_defaults()
    {
        var options = new PluginOptions();

        Assert.True(options.AutoStart);
        Assert.Equal("plugins", options.Discovery.RootPath);
        Assert.Equal(PluginConstants.DefaultHeartbeatIntervalSeconds, options.Health.HeartbeatIntervalSeconds);
        Assert.Equal(PluginConstants.DefaultFailureThreshold, options.Health.FailureThreshold);
    }

    [Fact]
    public void ConfigurationProvider_reads_a_plugins_section_and_toggle()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Plugins:Configuration:energy:Enabled"] = "false",
                ["Plugins:Configuration:energy:Threshold"] = "42",
            })
            .Build();
        var provider = new PluginConfigurationProvider(configuration);

        var energy = provider.GetConfiguration("energy");
        Assert.False(energy.Enabled);
        Assert.Equal("42", energy.Get("Threshold"));
        Assert.True(energy.Has("Threshold"));

        // A plugin with no section is enabled by default with no settings.
        var missing = provider.GetConfiguration("quality");
        Assert.True(missing.Enabled);
        Assert.Empty(missing.Values);
    }

    // ---- Metadata & capabilities ----------------------------------------------

    [Fact]
    public void Metadata_projects_a_manifest()
    {
        var metadata = PluginMetadata.FromManifest(Manifest("energy", "energy.telemetry"));

        Assert.Equal("energy", metadata.Key);
        Assert.Equal(PluginVersion.Parse("1.0.0"), metadata.Version);
        Assert.Contains("energy.telemetry", metadata.Capabilities);
    }

    [Fact]
    public void Capability_validation_checks_the_provided_surface()
    {
        var manifests = new[] { Manifest("energy", "energy.telemetry"), Manifest("oee", "oee.metrics") };

        Assert.True(PluginCapabilityValidator.ValidateRequired(manifests, ["energy.telemetry", "oee.metrics"]).IsSuccess);
        var missing = PluginCapabilityValidator.ValidateRequired(manifests, ["quality.audit"]);
        Assert.True(missing.IsFailure);
        Assert.Equal("Plugin.Capability.Missing", missing.Error.Code);
    }

    // ---- Activation -----------------------------------------------------------

    private sealed class KeylessMismatch : PluginBase
    {
        public override string Key => "actual";
    }

    [Fact]
    public void Activator_activates_a_type_and_enforces_the_key()
    {
        var activator = new PluginActivator();

        var ok = activator.Activate(typeof(KeylessMismatch), "actual");
        Assert.True(ok.IsSuccess);
        Assert.Equal("actual", ok.Value.Key);

        Assert.Equal("Plugin.Activate.KeyMismatch", activator.Activate(typeof(KeylessMismatch), "expected").Error.Code);
        Assert.Equal("Plugin.Activate.NotAPlugin", activator.Activate(typeof(string), "x").Error.Code);
    }

    // ---- Health ---------------------------------------------------------------

    [Fact]
    public void Health_tracks_heartbeats_failures_and_recovery()
    {
        var clock = new MutableClock(Now);
        var service = new PluginHealthService(clock, Options(o =>
        {
            o.Health.HeartbeatIntervalSeconds = 30;
            o.Health.UnhealthyAfterMissedHeartbeats = 3;
            o.Health.FailureThreshold = 3;
        }));

        Assert.Equal(PluginHealthStatus.Unknown, service.GetHealth("energy").Status);

        service.Heartbeat("energy");
        Assert.Equal(PluginHealthStatus.Healthy, service.GetHealth("energy").Status);

        service.ReportFailure("energy", "boom");
        Assert.Equal(PluginHealthStatus.Degraded, service.GetHealth("energy").Status);

        PluginHealth? recovered = null;
        service.Recovered += (_, e) => recovered = e.Health;

        service.ReportFailure("energy", "boom");
        service.ReportFailure("energy", "boom"); // threshold reached → latched unhealthy
        Assert.Equal(PluginHealthStatus.Unhealthy, service.GetHealth("energy").Status);

        service.Heartbeat("energy"); // recovers
        Assert.Equal(PluginHealthStatus.Healthy, service.GetHealth("energy").Status);
        Assert.NotNull(recovered);
        Assert.Equal("energy", recovered!.Key);
    }

    [Fact]
    public void Health_detects_a_stale_heartbeat()
    {
        var clock = new MutableClock(Now);
        var service = new PluginHealthService(clock, Options(o =>
        {
            o.Health.HeartbeatIntervalSeconds = 30;
            o.Health.UnhealthyAfterMissedHeartbeats = 3;
        }));

        service.Heartbeat("energy");
        clock.Advance(TimeSpan.FromSeconds(91)); // past 3 × 30s
        Assert.Equal(PluginHealthStatus.Unhealthy, service.GetHealth("energy").Status);
    }

    // ---- Manager lifecycle ----------------------------------------------------

    private static PluginManager NewManager(IPluginRegistry registry, IDateTimeProvider clock) =>
        new(
            registry,
            new PluginConfigurationProvider(new ConfigurationBuilder().Build()),
            new PluginHealthService(clock, Options()));

    [Fact]
    public async Task Manager_drives_the_full_lifecycle()
    {
        var plugin = new LifecyclePlugin("energy");
        var registry = new PluginRegistry();
        var descriptor = Loaded(plugin);
        registry.Register(descriptor);
        var manager = NewManager(registry, new MutableClock(Now));

        Assert.True((await manager.InitializeAsync("energy")).IsSuccess);
        Assert.True((await manager.StartAsync("energy")).IsSuccess);
        Assert.Equal(PluginState.Started, descriptor.State);

        Assert.True((await manager.StopAsync("energy")).IsSuccess);
        Assert.Equal(PluginState.Loaded, descriptor.State);

        Assert.True((await manager.ReloadAsync("energy")).IsSuccess);
        Assert.Equal(PluginState.Started, descriptor.State);

        Assert.True((await manager.UnloadAsync("energy")).IsSuccess);
        Assert.Equal(PluginState.Discovered, descriptor.State);

        Assert.Equal(
            ["init:energy", "start", "stop", "stop", "start", "stop", "unload"],
            plugin.Log);
    }

    [Fact]
    public async Task Manager_reports_unknown_and_unloaded_plugins()
    {
        var registry = new PluginRegistry();
        var manager = NewManager(registry, new MutableClock(Now));

        Assert.Equal("Plugin.Manager.NotFound", (await manager.StartAsync("ghost")).Error.Code);

        registry.Register(new PluginDescriptor(Manifest("energy"))); // discovered, no instance
        Assert.Equal("Plugin.Manager.NotLoaded", (await manager.StartAsync("energy")).Error.Code);
    }

    // ---- Catalog --------------------------------------------------------------

    [Fact]
    public void Catalog_projects_metadata_capabilities_and_health()
    {
        var registry = new PluginRegistry();
        registry.Register(new PluginDescriptor(Manifest("energy", "energy.telemetry")));
        registry.Register(new PluginDescriptor(Manifest("oee", "oee.metrics")));
        var health = new PluginHealthService(new MutableClock(Now), Options());
        health.Heartbeat("energy");
        var catalog = new PluginCatalog(registry, health);

        Assert.Equal(2, catalog.List().Count);
        Assert.NotNull(catalog.Find("energy"));
        Assert.Single(catalog.WithCapability("energy.telemetry"));
        Assert.Contains(catalog.Health(), h => h.Key == "energy" && h.Status == PluginHealthStatus.Healthy);
    }

    // ---- Dependency injection -------------------------------------------------

    [Fact]
    public void AddPluginFramework_registers_the_foundation_services()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Plugins:Health:FailureThreshold"] = "5",
                ["Plugins:Discovery:RootPath"] = "custom-plugins",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton<IDateTimeProvider>(new MutableClock(Now));
        services.AddPluginFramework(configuration);

        using var provider = services.BuildServiceProvider(validateScopes: true);

        Assert.IsType<PluginActivator>(provider.GetRequiredService<IPluginActivator>());
        Assert.IsType<PluginConfigurationProvider>(provider.GetRequiredService<IPluginConfigurationProvider>());
        Assert.IsType<PluginHealthService>(provider.GetRequiredService<IPluginHealthService>());
        Assert.IsType<PluginCatalog>(provider.GetRequiredService<IPluginCatalog>());
        Assert.IsType<PluginManager>(provider.GetRequiredService<IPluginManager>());

        var options = provider.GetRequiredService<IOptions<PluginOptions>>().Value;
        Assert.Equal(5, options.Health.FailureThreshold);
        Assert.Equal("custom-plugins", options.Discovery.RootPath);
    }
}
