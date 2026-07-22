using FactoryOS.Connectors.Runtime.Configuration;
using FactoryOS.Connectors.Runtime.Domain;
using FactoryOS.Contracts.Connectors;

namespace FactoryOS.Connectors.Runtime.Execution;

/// <summary>
/// Makes an existing <see cref="IConnector"/> invocable by the runtime, under the conventional
/// <c>read</c> operation.
/// <para>
/// This adapter is why not one of the connectors already in the repository had to change. The connector
/// contract stays exactly what it was — a tenant-scoped record stream — and the runtime's retries, circuit
/// breaker, rate limit, cache, authorization, audit and metrics wrap around it from the outside.
/// </para>
/// </summary>
public sealed class InboundConnectorOperationHandler : IConnectorOperationHandler
{
    private readonly IConnector _connector;

    /// <summary>Initializes a new instance of the <see cref="InboundConnectorOperationHandler"/> class.</summary>
    /// <param name="connector">The connector to adapt.</param>
    public InboundConnectorOperationHandler(IConnector connector)
    {
        ArgumentNullException.ThrowIfNull(connector);
        _connector = connector;
    }

    /// <inheritdoc />
    public string ConnectorKey => _connector.Key;

    /// <inheritdoc />
    public bool CanHandle(string operation) =>
        string.Equals(operation, ConnectorRuntimeConstants.ReadOperation, StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc />
    public async Task<ConnectorResponse> ExecuteAsync(
        ConnectorInvocation invocation, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(invocation);

        // The tenant travels from the invocation, never from a parameter a caller could set: the read context
        // a connector receives is the one the runtime already authorized, not one the request asked for.
        var parameters = invocation.Request.Parameters
            .Where(pair => pair.Value is not null)
            .ToDictionary(pair => pair.Key, pair => pair.Value!, StringComparer.OrdinalIgnoreCase);

        var context = new ConnectorReadContext(invocation.Tenant, parameters);

        var records = new List<SourceRecord>();
        await foreach (var record in _connector.ReadAsync(context, cancellationToken).ConfigureAwait(false))
        {
            records.Add(record);
        }

        return ConnectorResponse.Ok(records)
        with
        {
            Metadata = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["records"] = records.Count.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["sourceSystem"] = _connector.SourceSystem,
            },
        };
    }
}

/// <summary>
/// Makes an existing <see cref="IOutboundConnector"/> invocable by the runtime, under the conventional
/// <c>deliver</c> operation — the door out, wrapped by the same pipeline as the door in.
/// </summary>
public sealed class OutboundConnectorOperationHandler : IConnectorOperationHandler
{
    private readonly IOutboundConnector _connector;

    /// <summary>Initializes a new instance of the <see cref="OutboundConnectorOperationHandler"/> class.</summary>
    /// <param name="connector">The outbound connector to adapt.</param>
    public OutboundConnectorOperationHandler(IOutboundConnector connector)
    {
        ArgumentNullException.ThrowIfNull(connector);
        _connector = connector;
    }

    /// <inheritdoc />
    public string ConnectorKey => _connector.Key;

    /// <inheritdoc />
    public bool CanHandle(string operation) =>
        string.Equals(operation, ConnectorRuntimeConstants.DeliverOperation, StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc />
    public async Task<ConnectorResponse> ExecuteAsync(
        ConnectorInvocation invocation, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(invocation);

        if (invocation.Request.Payload is not OutboundMessage message)
        {
            return ConnectorResponse.Failed(ConnectorError.Validation(
                "Connector.Deliver.NoMessage",
                $"Operation '{invocation.Operation.Name}' needs an {nameof(OutboundMessage)} payload."));
        }

        if (!string.Equals(message.Tenant, invocation.Tenant, StringComparison.OrdinalIgnoreCase))
        {
            // The message names a tenant and so does the invocation. If they disagree, one of them is wrong,
            // and delivering it would mean sending one factory's notification through another's transport.
            return ConnectorResponse.Failed(ConnectorError.Forbidden(
                "Connector.Deliver.TenantMismatch",
                $"The message belongs to tenant '{message.Tenant}' but was delivered through "
                + $"'{invocation.Tenant}'."));
        }

        var result = await _connector.DeliverAsync(message, cancellationToken).ConfigureAwait(false);

        return result.Delivered
            ? ConnectorResponse.Ok(result) with
            {
                Metadata = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["transport"] = _connector.Transport,
                    ["detail"] = result.Detail,
                },
            }
            : ConnectorResponse.Failed(ConnectorError.Transient(
                "Connector.Deliver.Failed", result.Detail ?? "The transport did not deliver the message."));
    }
}
