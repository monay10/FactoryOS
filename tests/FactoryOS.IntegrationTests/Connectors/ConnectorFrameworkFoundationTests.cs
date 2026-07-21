using System.Runtime.CompilerServices;
using FactoryOS.Connectors.Framework.Catalog;
using FactoryOS.Connectors.Framework.Configuration;
using FactoryOS.Connectors.Framework.Health;
using FactoryOS.Connectors.Framework.Hosting;
using FactoryOS.Connectors.Framework.Lifecycle;
using FactoryOS.Connectors.Framework.Management;
using FactoryOS.Connectors.Framework.Registry;
using FactoryOS.Connectors.Framework.Runtime;
using FactoryOS.Connectors.Framework.Security;
using FactoryOS.Contracts.Connectors;
using FactoryOS.Domain.Abstractions;
using FactoryOS.Domain.Time;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FactoryOS.IntegrationTests.Connectors;

/// <summary>
/// The connector platform composed through <c>AddConnectorFramework(configuration)</c> against a real
/// container: several connectors register, initialize, connect and disconnect through the host and manager,
/// the catalog projects their metadata and health, a connector reconnects in place, and an encrypted
/// per-connector secret is decrypted with the configured key.
/// </summary>
public sealed class ConnectorFrameworkFoundationTests
{
    private static readonly string Base64Key = Convert.ToBase64String([.. Enumerable.Range(0, 32).Select(i => (byte)i)]);

    private sealed class CountingConnector : IConnector, IConnectorLifecycle
    {
        private readonly string _key;

        public CountingConnector(string key) => _key = key;

        public int Connects { get; private set; }

        public int Disconnects { get; private set; }

        public string Key => _key;

        public string SourceSystem => "test";

        public async IAsyncEnumerable<SourceRecord> ReadAsync(
            ConnectorReadContext context, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
            yield break;
        }

        public Task InitializeAsync(IConnectorContext context, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task ConnectAsync(CancellationToken cancellationToken)
        {
            Connects++;
            return Task.CompletedTask;
        }

        public Task DisconnectAsync(CancellationToken cancellationToken)
        {
            Disconnects++;
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private static ServiceProvider BuildProvider()
    {
        var secret = AesGcmConnectorSecretProtector.FromBase64Key(Base64Key).Protect("db-password");
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Connectors:Security:EncryptionKey"] = Base64Key,
                ["Connectors:Configuration:logo:Password"] = secret,
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();
        services.AddConnectorFramework(configuration);
        return services.BuildServiceProvider(validateScopes: true);
    }

    private static void Register(IConnectorRegistry registry, IConnector instance, ConnectorCapability capabilities)
    {
        var descriptor = new ConnectorDescriptor(
            new ConnectorManifest { Key = instance.Key, Name = instance.Key, SourceSystem = "test", Provides = ["Asset"] },
            new ConnectorVersion(1, 0, 0),
            capabilities);
        descriptor.AttachInstance(instance);
        registry.Register(descriptor);
    }

    [Fact]
    public async Task Multiple_connectors_connect_reconnect_and_disconnect_through_the_host()
    {
        using var provider = BuildProvider();

        var registry = provider.GetRequiredService<IConnectorRegistry>();
        var logo = new CountingConnector("logo");
        var webhook = new CountingConnector("webhook");
        Register(registry, logo, ConnectorCapability.Read);
        Register(registry, webhook, ConnectorCapability.Write);

        var host = provider.GetRequiredService<IConnectorHost>();
        var manager = provider.GetRequiredService<IConnectorManager>();
        var catalog = provider.GetRequiredService<IConnectorCatalog>();

        Assert.True((await host.ConnectAllAsync()).IsSuccess);
        Assert.Equal(ConnectorState.Connected, registry.Find("logo")!.State);
        Assert.Equal(ConnectorState.Connected, registry.Find("webhook")!.State);
        Assert.Equal(2, catalog.Health().Count(h => h.Status == ConnectorHealthStatus.Healthy));
        Assert.Single(catalog.WithCapability(ConnectorCapability.Write));

        Assert.True((await manager.ReconnectAsync("logo")).IsSuccess);
        Assert.Equal(2, logo.Connects);
        Assert.Equal(1, logo.Disconnects);

        Assert.True((await host.DisconnectAllAsync()).IsSuccess);
        Assert.Equal(ConnectorState.Disconnected, registry.Find("logo")!.State);
        Assert.Equal(1, webhook.Disconnects);
    }

    [Fact]
    public void An_encrypted_connector_secret_is_decrypted_with_the_configured_key()
    {
        using var provider = BuildProvider();

        var configuration = provider.GetRequiredService<IConnectorConfigurationProvider>().GetConfiguration("logo");

        Assert.True(configuration.Enabled);
        Assert.Equal("db-password", configuration.GetSecret("Password"));
        Assert.NotEqual("db-password", configuration.Get("Password")); // stored encrypted
    }
}
