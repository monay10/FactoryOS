using FactoryOS.Connectors.Framework.Configuration;
using FactoryOS.Connectors.Framework.Health;
using FactoryOS.Connectors.Framework.Lifecycle;
using FactoryOS.Connectors.Framework.Registry;
using FactoryOS.Connectors.Framework.Runtime;
using FactoryOS.Contracts.Connectors;
using FactoryOS.Domain.Results;

namespace FactoryOS.Connectors.Framework.Management;

/// <summary>
/// Drives a single connector through its connection lifecycle — Initialize, Connect, Disconnect, Reconnect
/// and Dispose — over the registry descriptors, honouring the optional <see cref="IConnectorLifecycle"/> and
/// recording heartbeats with the health service. A connector that does not implement the lifecycle is still
/// tracked; its connect/disconnect steps are no-ops.
/// </summary>
public interface IConnectorManager
{
    /// <summary>Gets the descriptors of all known connectors.</summary>
    IReadOnlyCollection<ConnectorDescriptor> Connectors { get; }

    /// <summary>Initializes a connector, invoking its lifecycle hook when it implements one.</summary>
    /// <param name="key">The connector key.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A successful result, or a failure describing why the connector could not be initialized.</returns>
    Task<Result> InitializeAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>Connects a connector and records its first heartbeat.</summary>
    /// <param name="key">The connector key.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A successful result, or a failure.</returns>
    Task<Result> ConnectAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>Disconnects a connected connector.</summary>
    /// <param name="key">The connector key.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A successful result, or a failure.</returns>
    Task<Result> DisconnectAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>Reconnects a connector in place: disconnects it, then connects it again.</summary>
    /// <param name="key">The connector key.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A successful result, or a failure.</returns>
    Task<Result> ReconnectAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>Disconnects (if connected) and disposes a connector, returning it to the discovered state.</summary>
    /// <param name="key">The connector key.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A successful result, or a failure.</returns>
    Task<Result> DisposeAsync(string key, CancellationToken cancellationToken = default);
}

/// <summary>Default <see cref="IConnectorManager"/>.</summary>
public sealed class ConnectorManager : IConnectorManager
{
    private readonly IConnectorRegistry _registry;
    private readonly IConnectorConfigurationProvider _configuration;
    private readonly IConnectorHealthService _health;

    /// <summary>Initializes a new instance of the <see cref="ConnectorManager"/> class.</summary>
    /// <param name="registry">The connector registry.</param>
    /// <param name="configuration">The connector configuration provider.</param>
    /// <param name="health">The connector health service.</param>
    public ConnectorManager(
        IConnectorRegistry registry,
        IConnectorConfigurationProvider configuration,
        IConnectorHealthService health)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(health);

        _registry = registry;
        _configuration = configuration;
        _health = health;
    }

    /// <inheritdoc />
    public IReadOnlyCollection<ConnectorDescriptor> Connectors => _registry.All;

    /// <inheritdoc />
    public async Task<Result> InitializeAsync(string key, CancellationToken cancellationToken = default)
    {
        var resolved = Resolve(key);
        if (resolved.IsFailure)
        {
            return resolved;
        }

        var (descriptor, instance) = resolved.Value;
        if (instance is IConnectorLifecycle lifecycle)
        {
            var context = new ConnectorContext(descriptor, _configuration.GetConfiguration(key));
            await lifecycle.InitializeAsync(context, cancellationToken).ConfigureAwait(false);
        }

        descriptor.MarkInitialized();
        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result> ConnectAsync(string key, CancellationToken cancellationToken = default)
    {
        var resolved = Resolve(key);
        if (resolved.IsFailure)
        {
            return resolved;
        }

        var (descriptor, instance) = resolved.Value;
        if (instance is IConnectorLifecycle lifecycle)
        {
            await lifecycle.ConnectAsync(cancellationToken).ConfigureAwait(false);
        }

        descriptor.MarkConnected();
        _health.Heartbeat(key);
        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result> DisconnectAsync(string key, CancellationToken cancellationToken = default)
    {
        var resolved = Resolve(key);
        if (resolved.IsFailure)
        {
            return resolved;
        }

        var (descriptor, instance) = resolved.Value;
        if (instance is IConnectorLifecycle lifecycle)
        {
            await lifecycle.DisconnectAsync(cancellationToken).ConfigureAwait(false);
        }

        descriptor.MarkDisconnected();
        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result> ReconnectAsync(string key, CancellationToken cancellationToken = default)
    {
        var disconnect = await DisconnectAsync(key, cancellationToken).ConfigureAwait(false);
        return disconnect.IsFailure ? disconnect : await ConnectAsync(key, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<Result> DisposeAsync(string key, CancellationToken cancellationToken = default)
    {
        var resolved = Resolve(key);
        if (resolved.IsFailure)
        {
            return resolved;
        }

        var (descriptor, instance) = resolved.Value;
        if (descriptor.State == ConnectorState.Connected && instance is IConnectorLifecycle connected)
        {
            await connected.DisconnectAsync(cancellationToken).ConfigureAwait(false);
            descriptor.MarkDisconnected();
        }

        if (instance is IConnectorLifecycle lifecycle)
        {
            await lifecycle.DisposeAsync().ConfigureAwait(false);
        }

        descriptor.MarkDiscovered();
        return Result.Success();
    }

    private Result<(ConnectorDescriptor Descriptor, IConnector Instance)> Resolve(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var descriptor = _registry.Find(key);
        if (descriptor is null)
        {
            return Result.Failure<(ConnectorDescriptor, IConnector)>(
                Error.NotFound("Connector.Manager.NotFound", $"No connector with key '{key}' is registered."));
        }

        if (descriptor.Instance is null)
        {
            return Result.Failure<(ConnectorDescriptor, IConnector)>(Error.Validation(
                "Connector.Manager.NotAttached", $"Connector '{key}' has no attached instance to manage."));
        }

        return (descriptor, descriptor.Instance);
    }
}
