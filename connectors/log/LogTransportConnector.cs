using FactoryOS.Connectors.Log.Domain;
using FactoryOS.Contracts.Connectors;

namespace FactoryOS.Connectors.Log;

/// <summary>
/// An outbound connector whose transport is a journal — it "delivers" a notification by appending it to the log
/// journal and reporting success. The simplest possible door out: no external system, useful as a default sink,
/// an audit trail, and the reference implementation every other outbound connector (webhook, email, SMS) follows.
/// </summary>
public sealed class LogTransportConnector : IOutboundConnector
{
    /// <summary>The connector key, matching <c>connector.json</c>.</summary>
    public const string ConnectorKey = "log";

    private readonly IDeliveryJournal _journal;
    private readonly LogConnectorOptions _options;

    /// <summary>Initializes a new instance of the <see cref="LogTransportConnector"/> class.</summary>
    /// <param name="journal">The journal to deliver into.</param>
    /// <param name="options">The connector options carrying the transport name.</param>
    public LogTransportConnector(IDeliveryJournal journal, LogConnectorOptions options)
    {
        ArgumentNullException.ThrowIfNull(journal);
        ArgumentNullException.ThrowIfNull(options);
        _journal = journal;
        _options = options;
    }

    /// <inheritdoc />
    public string Key => ConnectorKey;

    /// <inheritdoc />
    public string Transport => _options.Transport;

    /// <inheritdoc />
    public Task<OutboundResult> DeliverAsync(OutboundMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        _journal.Record(
            message.Tenant,
            new DeliveryRecord(message.Channel, message.Priority, message.Subject, message.Action, message.OccurredAt));

        return Task.FromResult(OutboundResult.Ok("journaled"));
    }
}
