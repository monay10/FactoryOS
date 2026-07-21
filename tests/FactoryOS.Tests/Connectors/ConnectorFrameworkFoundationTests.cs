using FactoryOS.Connectors.Framework.Activation;
using FactoryOS.Connectors.Framework.Catalog;
using FactoryOS.Connectors.Framework.Configuration;
using FactoryOS.Connectors.Framework.Health;
using FactoryOS.Connectors.Framework.Lifecycle;
using FactoryOS.Connectors.Framework.Management;
using FactoryOS.Connectors.Framework.Registry;
using FactoryOS.Connectors.Framework.Runtime;
using FactoryOS.Connectors.Framework.Security;
using FactoryOS.Connectors.Manifest;
using FactoryOS.Contracts.Connectors;
using FactoryOS.Domain.Abstractions;
using FactoryOS.Tests.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace FactoryOS.Tests.Connectors;

public sealed class ConnectorFrameworkFoundationTests
{
    private static readonly DateTimeOffset Now = new(2026, 07, 21, 12, 00, 00, TimeSpan.Zero);

    private static readonly byte[] Key = [.. Enumerable.Range(0, 32).Select(i => (byte)i)];

    private static IOptions<ConnectorOptions> Options(Action<ConnectorOptions>? configure = null)
    {
        var options = new ConnectorOptions();
        configure?.Invoke(options);
        return Microsoft.Extensions.Options.Options.Create(options);
    }

    private sealed class RecordingConnector : IConnector, IConnectorLifecycle, IConnectorHealthCheck
    {
        private readonly string _key;

        public RecordingConnector(string key) => _key = key;

        public List<string> Log { get; } = [];

        public string Key => _key;

        public string SourceSystem => "test";

        public async IAsyncEnumerable<SourceRecord> ReadAsync(
            ConnectorReadContext context,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
            yield break;
        }

        public Task InitializeAsync(IConnectorContext context, CancellationToken cancellationToken)
        {
            Log.Add($"init:{context.Key}");
            return Task.CompletedTask;
        }

        public Task ConnectAsync(CancellationToken cancellationToken)
        {
            Log.Add("connect");
            return Task.CompletedTask;
        }

        public Task DisconnectAsync(CancellationToken cancellationToken)
        {
            Log.Add("disconnect");
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            Log.Add("dispose");
            return ValueTask.CompletedTask;
        }

        public ConnectorHealthStatus Check() => ConnectorHealthStatus.Healthy;
    }

    private static ConnectorManifest Manifest(string key, params string[] provides) => new()
    {
        Key = key,
        Name = key,
        SourceSystem = "test",
        Provides = provides,
    };

    private static ConnectorDescriptor Attached(IConnector instance, ConnectorCapability capabilities)
    {
        var descriptor = new ConnectorDescriptor(Manifest(instance.Key), new ConnectorVersion(1, 0, 0), capabilities);
        descriptor.AttachInstance(instance);
        return descriptor;
    }

    // ---- Version --------------------------------------------------------------

    [Fact]
    public void Version_parses_and_orders()
    {
        Assert.Equal(new ConnectorVersion(2, 1, 0), ConnectorVersion.Parse("2.1.0"));
        Assert.True(ConnectorVersion.Parse("1.2.0") < ConnectorVersion.Parse("1.10.0"));
        Assert.False(ConnectorVersion.TryParse("1.2", out _));
    }

    // ---- Capability -----------------------------------------------------------

    [Fact]
    public void Capability_flags_combine_and_test()
    {
        var caps = ConnectorCapabilities.Parse("Read, Events | Streaming");

        Assert.True(caps.Supports(ConnectorCapability.Read));
        Assert.True(caps.Supports(ConnectorCapability.Events | ConnectorCapability.Streaming));
        Assert.False(caps.Supports(ConnectorCapability.Write));
        Assert.False(ConnectorCapability.None.Supports(ConnectorCapability.None));
    }

    // ---- Manifest reuse -------------------------------------------------------

    [Fact]
    public void A_read_manifest_flows_into_a_descriptor_and_metadata()
    {
        var manifest = ConnectorManifestReader.Read(
            """{ "key": "logo", "name": "Logo", "sourceSystem": "Logo", "provides": ["InventoryItem"] }""");
        Assert.True(manifest.IsSuccess);

        var descriptor = new ConnectorDescriptor(
            manifest.Value, new ConnectorVersion(1, 2, 3), ConnectorCapability.Read | ConnectorCapability.Events);
        var metadata = ConnectorMetadata.FromDescriptor(descriptor);

        Assert.Equal("logo", metadata.Key);
        Assert.Equal("Logo", metadata.SourceSystem);
        Assert.Equal(new ConnectorVersion(1, 2, 3), metadata.Version);
        Assert.Contains("InventoryItem", metadata.Provides);
        Assert.True(metadata.Capabilities.Supports(ConnectorCapability.Events));
    }

    // ---- Encrypted settings ---------------------------------------------------

    [Fact]
    public void Aes_protector_round_trips_a_secret_and_passthrough_is_inert()
    {
        var aes = new AesGcmConnectorSecretProtector(Key);
        var protectedValue = aes.Protect("s3cr3t!");

        Assert.True(aes.IsProtected(protectedValue));
        Assert.Equal("s3cr3t!", aes.Unprotect(protectedValue));

        var passthrough = new PassthroughConnectorSecretProtector();
        Assert.False(passthrough.IsProtected(protectedValue));
        Assert.Equal("plain", passthrough.Unprotect("plain"));

        Assert.Throws<ArgumentException>(() => new AesGcmConnectorSecretProtector([1, 2, 3]));
    }

    [Fact]
    public void ConfigurationProvider_reads_settings_and_decrypts_secrets()
    {
        var aes = new AesGcmConnectorSecretProtector(Key);
        var secret = aes.Protect("db-password");
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Connectors:Configuration:logo:Enabled"] = "false",
                ["Connectors:Configuration:logo:Host"] = "10.0.0.5",
                ["Connectors:Configuration:logo:Password"] = secret,
            })
            .Build();
        var provider = new ConnectorConfigurationProvider(configuration, aes);

        var logo = provider.GetConfiguration("logo");
        Assert.False(logo.Enabled);
        Assert.Equal("10.0.0.5", logo.Get("Host"));
        Assert.Equal(secret, logo.Get("Password"));          // stored still encrypted
        Assert.Equal("db-password", logo.GetSecret("Password")); // decrypted on demand

        var missing = provider.GetConfiguration("sap");
        Assert.True(missing.Enabled);
        Assert.Empty(missing.Values);
    }

    // ---- Activation -----------------------------------------------------------

    [Fact]
    public void Activator_activates_a_type_and_enforces_the_key()
    {
        var activator = new ConnectorActivator();

        var ok = activator.Activate(typeof(ActivatableConnector), "sample");
        Assert.True(ok.IsSuccess);
        Assert.Equal("Connector.Activate.KeyMismatch", activator.Activate(typeof(ActivatableConnector), "other").Error.Code);
        Assert.Equal("Connector.Activate.NotAConnector", activator.Activate(typeof(string), "x").Error.Code);
    }

    private sealed class ActivatableConnector : IConnector
    {
        public string Key => "sample";

        public string SourceSystem => "test";

        public async IAsyncEnumerable<SourceRecord> ReadAsync(
            ConnectorReadContext context,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
            yield break;
        }
    }

    // ---- Health ---------------------------------------------------------------

    [Fact]
    public void Health_tracks_heartbeats_failures_and_recovery()
    {
        var clock = new MutableClock(Now);
        var service = new ConnectorHealthService(clock, Options(o => o.Health.FailureThreshold = 3));

        Assert.Equal(ConnectorHealthStatus.Unknown, service.GetHealth("logo").Status);
        service.Heartbeat("logo");
        Assert.Equal(ConnectorHealthStatus.Healthy, service.GetHealth("logo").Status);

        service.ReportFailure("logo", "timeout");
        Assert.Equal(ConnectorHealthStatus.Degraded, service.GetHealth("logo").Status);

        ConnectorHealth? recovered = null;
        service.Recovered += (_, e) => recovered = e.Health;
        service.ReportFailure("logo", "timeout");
        service.ReportFailure("logo", "timeout");
        Assert.Equal(ConnectorHealthStatus.Unhealthy, service.GetHealth("logo").Status);

        service.Heartbeat("logo");
        Assert.Equal(ConnectorHealthStatus.Healthy, service.GetHealth("logo").Status);
        Assert.NotNull(recovered);
        Assert.Equal("logo", recovered!.Key);
    }

    [Fact]
    public void Health_detects_a_stale_heartbeat()
    {
        var clock = new MutableClock(Now);
        var service = new ConnectorHealthService(clock, Options(o =>
        {
            o.Health.HeartbeatIntervalSeconds = 30;
            o.Health.UnhealthyAfterMissedHeartbeats = 3;
        }));

        service.Heartbeat("logo");
        clock.Advance(TimeSpan.FromSeconds(91));
        Assert.Equal(ConnectorHealthStatus.Unhealthy, service.GetHealth("logo").Status);
    }

    // ---- Manager lifecycle ----------------------------------------------------

    private static ConnectorManager NewManager(IConnectorRegistry registry, IDateTimeProvider clock) =>
        new(
            registry,
            new ConnectorConfigurationProvider(new ConfigurationBuilder().Build(), new PassthroughConnectorSecretProtector()),
            new ConnectorHealthService(clock, Options()));

    [Fact]
    public async Task Manager_drives_the_full_connection_lifecycle()
    {
        var connector = new RecordingConnector("logo");
        var registry = new ConnectorRegistry();
        var descriptor = Attached(connector, ConnectorCapability.Read);
        registry.Register(descriptor);
        var manager = NewManager(registry, new MutableClock(Now));

        Assert.True((await manager.InitializeAsync("logo")).IsSuccess);
        Assert.Equal(ConnectorState.Initialized, descriptor.State);

        Assert.True((await manager.ConnectAsync("logo")).IsSuccess);
        Assert.Equal(ConnectorState.Connected, descriptor.State);

        Assert.True((await manager.DisconnectAsync("logo")).IsSuccess);
        Assert.Equal(ConnectorState.Disconnected, descriptor.State);

        Assert.True((await manager.ReconnectAsync("logo")).IsSuccess);
        Assert.Equal(ConnectorState.Connected, descriptor.State);

        Assert.True((await manager.DisposeAsync("logo")).IsSuccess);
        Assert.Equal(ConnectorState.Discovered, descriptor.State);

        Assert.Equal(
            ["init:logo", "connect", "disconnect", "disconnect", "connect", "disconnect", "dispose"],
            connector.Log);
    }

    [Fact]
    public async Task Manager_reports_unknown_and_unattached_connectors()
    {
        var registry = new ConnectorRegistry();
        var manager = NewManager(registry, new MutableClock(Now));

        Assert.Equal("Connector.Manager.NotFound", (await manager.ConnectAsync("ghost")).Error.Code);

        registry.Register(new ConnectorDescriptor(Manifest("logo")));
        Assert.Equal("Connector.Manager.NotAttached", (await manager.ConnectAsync("logo")).Error.Code);
    }

    // ---- Catalog --------------------------------------------------------------

    [Fact]
    public void Catalog_projects_metadata_capabilities_and_health()
    {
        var registry = new ConnectorRegistry();
        registry.Register(Attached(new RecordingConnector("logo"), ConnectorCapability.Read | ConnectorCapability.Events));
        registry.Register(Attached(new RecordingConnector("webhook"), ConnectorCapability.Write));
        var health = new ConnectorHealthService(new MutableClock(Now), Options());
        health.Heartbeat("logo");
        var catalog = new ConnectorCatalog(registry, health);

        Assert.Equal(2, catalog.List().Count);
        Assert.NotNull(catalog.Find("logo"));
        Assert.Single(catalog.WithCapability(ConnectorCapability.Events));
        Assert.Single(catalog.WithCapability(ConnectorCapability.Write));
        Assert.Contains(catalog.Health(), h => h.Key == "logo" && h.Status == ConnectorHealthStatus.Healthy);
    }

    // ---- Dependency injection -------------------------------------------------

    [Fact]
    public void AddConnectorFramework_registers_the_platform_services()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Connectors:Health:FailureThreshold"] = "7",
                ["Connectors:Discovery:RootPath"] = "custom-connectors",
                ["Connectors:Security:EncryptionKey"] = Convert.ToBase64String(Key),
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton<IDateTimeProvider>(new MutableClock(Now));
        services.AddConnectorFramework(configuration);

        using var provider = services.BuildServiceProvider(validateScopes: true);

        Assert.IsType<ConnectorRegistry>(provider.GetRequiredService<IConnectorRegistry>());
        Assert.IsType<ConnectorActivator>(provider.GetRequiredService<IConnectorActivator>());
        Assert.IsType<ConnectorConfigurationProvider>(provider.GetRequiredService<IConnectorConfigurationProvider>());
        Assert.IsType<ConnectorHealthService>(provider.GetRequiredService<IConnectorHealthService>());
        Assert.IsType<ConnectorCatalog>(provider.GetRequiredService<IConnectorCatalog>());
        Assert.IsType<ConnectorManager>(provider.GetRequiredService<IConnectorManager>());

        // The configured AES key selects the encrypting protector.
        Assert.IsType<AesGcmConnectorSecretProtector>(provider.GetRequiredService<IConnectorSecretProtector>());

        var options = provider.GetRequiredService<IOptions<ConnectorOptions>>().Value;
        Assert.Equal(7, options.Health.FailureThreshold);
        Assert.Equal("custom-connectors", options.Discovery.RootPath);
    }
}
