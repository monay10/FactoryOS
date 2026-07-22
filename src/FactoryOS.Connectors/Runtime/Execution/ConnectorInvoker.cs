using System.Collections.Concurrent;
using System.Globalization;
using FactoryOS.Connectors.Runtime.Configuration;
using FactoryOS.Connectors.Runtime.Domain;
using Microsoft.Extensions.Options;

namespace FactoryOS.Connectors.Runtime.Execution;

/// <summary>
/// Performs one connector operation against its external system. This is the only place in the runtime that
/// actually touches the outside world; everything else decides whether, when and how often it should.
/// </summary>
public interface IConnectorOperationHandler
{
    /// <summary>Gets the definition key this handler serves.</summary>
    string ConnectorKey { get; }

    /// <summary>Determines whether the handler performs an operation.</summary>
    /// <param name="operation">The operation name.</param>
    /// <returns><see langword="true"/> when the handler performs it.</returns>
    bool CanHandle(string operation);

    /// <summary>Performs the operation.</summary>
    /// <param name="invocation">The invocation.</param>
    /// <param name="cancellationToken">A token to cancel the attempt.</param>
    /// <returns>The response.</returns>
    Task<ConnectorResponse> ExecuteAsync(ConnectorInvocation invocation, CancellationToken cancellationToken);
}

/// <summary>
/// The terminal stage of the pipeline: it finds the handler for the invocation's connector, applies the
/// attempt deadline, and turns anything the handler throws into a classified
/// <see cref="ConnectorError"/>.
/// <para>
/// Containing the throw here is deliberate. Above this line a failed ERP call is an ordinary value the
/// pipeline can meter, audit and decide about; below it, connectors are free to be written the way the
/// vendor's client library wants to be written.
/// </para>
/// </summary>
public sealed class ConnectorInvoker
{
    private readonly ConcurrentDictionary<string, IConnectorOperationHandler> _handlers =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly ConnectorRuntimeOptions _options;

    /// <summary>Initializes a new instance of the <see cref="ConnectorInvoker"/> class.</summary>
    /// <param name="handlers">The handlers registered at composition time.</param>
    /// <param name="options">The runtime options.</param>
    public ConnectorInvoker(IEnumerable<IConnectorOperationHandler> handlers, IOptions<ConnectorRuntimeOptions> options)
    {
        ArgumentNullException.ThrowIfNull(handlers);
        ArgumentNullException.ThrowIfNull(options);

        _options = options.Value;
        foreach (var handler in handlers)
        {
            _handlers[handler.ConnectorKey] = handler;
        }
    }

    /// <summary>Attaches a handler, replacing any handler already serving that connector.</summary>
    /// <param name="handler">The handler.</param>
    public void Attach(IConnectorOperationHandler handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        _handlers[handler.ConnectorKey] = handler;
    }

    /// <summary>Detaches the handler serving a connector.</summary>
    /// <param name="connectorKey">The definition key.</param>
    /// <returns><see langword="true"/> when a handler was detached.</returns>
    public bool Detach(string connectorKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectorKey);
        return _handlers.TryRemove(connectorKey, out _);
    }

    /// <summary>Finds the handler serving a connector.</summary>
    /// <param name="connectorKey">The definition key.</param>
    /// <returns>The handler, or <see langword="null"/> when none is attached.</returns>
    public IConnectorOperationHandler? Find(string connectorKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectorKey);
        return _handlers.TryGetValue(connectorKey, out var handler) ? handler : null;
    }

    /// <summary>Gets the connectors that currently have a handler attached, ordered by key.</summary>
    /// <returns>The definition keys.</returns>
    public IReadOnlyList<string> Attached() => [.. _handlers.Keys.OrderBy(key => key, StringComparer.Ordinal)];

    /// <summary>Performs one attempt.</summary>
    /// <param name="invocation">The invocation.</param>
    /// <param name="cancellationToken">A token to cancel the attempt.</param>
    /// <returns>The response.</returns>
    public async Task<ConnectorResponse> InvokeAsync(
        ConnectorInvocation invocation, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(invocation);

        var handler = Find(invocation.Definition.Key);
        if (handler is null)
        {
            return ConnectorResponse.Failed(ConnectorError.NotFound(
                "Connector.Invoke.NoHandler",
                $"No handler is attached for connector '{invocation.Definition.Key}'."));
        }

        if (!handler.CanHandle(invocation.Operation.Name))
        {
            return ConnectorResponse.Failed(ConnectorError.NotFound(
                "Connector.Invoke.UnsupportedOperation",
                $"Connector '{invocation.Definition.Key}' does not perform operation "
                + $"'{invocation.Operation.Name}'."));
        }

        invocation.BeginAttempt();

        var timeout = invocation.TimeoutOr(_options.DefaultTimeout);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(timeout);

        try
        {
            return await handler.ExecuteAsync(invocation, deadline.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return ConnectorResponse.Failed(ConnectorError.Timeout(
                "Connector.Invoke.Timeout",
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"Operation '{invocation.Operation.Name}' did not finish within {timeout}.")));
        }
        catch (OperationCanceledException)
        {
            return ConnectorResponse.Failed(new ConnectorError(
                "Connector.Invoke.Cancelled",
                $"Operation '{invocation.Operation.Name}' was cancelled by its caller.",
                ConnectorErrorKind.Cancelled));
        }
#pragma warning disable CA1031 // A connector reaches an external system; anything it throws must become a value.
        catch (Exception exception)
#pragma warning restore CA1031
        {
            return ConnectorResponse.Failed(Classify(exception, invocation.Operation.Name));
        }
    }

    /// <summary>
    /// Classifies an exception a connector let escape. A connector that knows better should return a typed
    /// error instead; this is the honest fallback for the ones that cannot, and it errs toward
    /// <see cref="ConnectorErrorKind.Permanent"/> so an unrecognised failure is never silently retried.
    /// </summary>
    private static ConnectorError Classify(Exception exception, string operation) => exception switch
    {
        TimeoutException => ConnectorError.Timeout(
            "Connector.Invoke.Timeout", $"Operation '{operation}' timed out: {exception.Message}"),
        IOException => ConnectorError.Transient(
            "Connector.Invoke.Io", $"Operation '{operation}' hit an I/O failure: {exception.Message}"),
        UnauthorizedAccessException => ConnectorError.Unauthorized(
            "Connector.Invoke.Unauthorized", $"Operation '{operation}' was refused: {exception.Message}"),
        ArgumentException or FormatException or InvalidOperationException => ConnectorError.Validation(
            "Connector.Invoke.Invalid", $"Operation '{operation}' was rejected: {exception.Message}"),
        _ => ConnectorError.Permanent(
            "Connector.Invoke.Failed", $"Operation '{operation}' failed: {exception.Message}"),
    };
}
