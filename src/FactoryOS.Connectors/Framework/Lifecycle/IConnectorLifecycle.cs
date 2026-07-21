using FactoryOS.Connectors.Framework.Runtime;

namespace FactoryOS.Connectors.Framework.Lifecycle;

/// <summary>
/// The optional connection lifecycle a connector may implement in addition to the read contract
/// <see cref="Contracts.Connectors.IConnector"/>. It adds initialization (with the connector context),
/// connect, disconnect and disposal so the connector manager can drive the full Initialize → Connect →
/// Disconnect → Reconnect → Dispose sequence. Connectors that do not implement it are still tracked, but
/// their connect/disconnect steps are no-ops.
/// </summary>
public interface IConnectorLifecycle : IAsyncDisposable
{
    /// <summary>Initializes the connector with its runtime context, before it connects.</summary>
    /// <param name="context">The connector context.</param>
    /// <param name="cancellationToken">A token to cancel initialization.</param>
    /// <returns>A task that completes when initialization has finished.</returns>
    Task InitializeAsync(IConnectorContext context, CancellationToken cancellationToken);

    /// <summary>Opens the connection to the source system.</summary>
    /// <param name="cancellationToken">A token to cancel the connect.</param>
    /// <returns>A task that completes when the connection is open.</returns>
    Task ConnectAsync(CancellationToken cancellationToken);

    /// <summary>Closes the connection to the source system.</summary>
    /// <param name="cancellationToken">A token to cancel the disconnect.</param>
    /// <returns>A task that completes when the connection is closed.</returns>
    Task DisconnectAsync(CancellationToken cancellationToken);
}
