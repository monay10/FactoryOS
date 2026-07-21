using FactoryOS.Connectors.Framework.Management;
using FactoryOS.Connectors.Framework.Registry;
using FactoryOS.Connectors.Framework.Runtime;
using FactoryOS.Domain.Results;

namespace FactoryOS.Connectors.Framework.Hosting;

/// <summary>
/// Orchestrates the connection lifecycle across every enabled connector: it initializes and connects them
/// on start-up and disconnects them on shutdown, delegating each transition to the connector manager.
/// Disabled and faulted connectors are skipped; a connector without an attached instance is skipped too.
/// </summary>
public interface IConnectorHost
{
    /// <summary>Gets the descriptors of all known connectors.</summary>
    IReadOnlyCollection<ConnectorDescriptor> Connectors { get; }

    /// <summary>Initializes and connects every enabled, attached connector.</summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A successful result, or a failure carrying the first connector that failed to connect.</returns>
    Task<Result> ConnectAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Disconnects every connected connector.</summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A successful result, or a failure carrying the first connector that failed to disconnect.</returns>
    Task<Result> DisconnectAllAsync(CancellationToken cancellationToken = default);
}

/// <summary>Default <see cref="IConnectorHost"/> driving the connectors through the manager.</summary>
public sealed class ConnectorHost : IConnectorHost
{
    private readonly IConnectorRegistry _registry;
    private readonly IConnectorManager _manager;

    /// <summary>Initializes a new instance of the <see cref="ConnectorHost"/> class.</summary>
    /// <param name="registry">The connector registry.</param>
    /// <param name="manager">The connector manager.</param>
    public ConnectorHost(IConnectorRegistry registry, IConnectorManager manager)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(manager);
        _registry = registry;
        _manager = manager;
    }

    /// <inheritdoc />
    public IReadOnlyCollection<ConnectorDescriptor> Connectors => _registry.All;

    /// <inheritdoc />
    public async Task<Result> ConnectAllAsync(CancellationToken cancellationToken = default)
    {
        foreach (var descriptor in Manageable())
        {
            var initialize = await _manager.InitializeAsync(descriptor.Key, cancellationToken).ConfigureAwait(false);
            if (initialize.IsFailure)
            {
                return initialize;
            }

            var connect = await _manager.ConnectAsync(descriptor.Key, cancellationToken).ConfigureAwait(false);
            if (connect.IsFailure)
            {
                return connect;
            }
        }

        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result> DisconnectAllAsync(CancellationToken cancellationToken = default)
    {
        foreach (var descriptor in _registry.All.Where(d => d.State == ConnectorState.Connected))
        {
            var disconnect = await _manager.DisconnectAsync(descriptor.Key, cancellationToken).ConfigureAwait(false);
            if (disconnect.IsFailure)
            {
                return disconnect;
            }
        }

        return Result.Success();
    }

    private IEnumerable<ConnectorDescriptor> Manageable() =>
        _registry.All.Where(descriptor =>
            descriptor.Instance is not null
            && descriptor.State is not (ConnectorState.Disabled or ConnectorState.Faulted));
}
