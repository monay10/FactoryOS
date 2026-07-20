using System.Globalization;
using FactoryOS.Contracts.Events;
using FactoryOS.Plugins.Dashboard.Domain;

namespace FactoryOS.Plugins.Dashboard.Application;

/// <summary>Pushes a <see cref="QualityLineQuarantined"/> onto the board as a warning alert — a manual quality hold is
/// worth surfacing on the wall. Consumes the shared event, never referencing the Quality module, and is idempotent by
/// event id so redelivery does not double the tile.</summary>
public sealed class QualityLineQuarantinedHandler : IEventHandler<QualityLineQuarantined>
{
    private readonly IOperationsBoard _board;
    private readonly IProcessedEventLog _processed;

    /// <summary>Initializes a new instance of the <see cref="QualityLineQuarantinedHandler"/> class.</summary>
    /// <param name="board">The operations read-model.</param>
    /// <param name="processed">The processed-event log for idempotency.</param>
    public QualityLineQuarantinedHandler(IOperationsBoard board, IProcessedEventLog processed)
    {
        ArgumentNullException.ThrowIfNull(board);
        ArgumentNullException.ThrowIfNull(processed);
        _board = board;
        _processed = processed;
    }

    /// <inheritdoc />
    public Task HandleAsync(QualityLineQuarantined integrationEvent, EventContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        ArgumentNullException.ThrowIfNull(context);

        if (_processed.TryMarkProcessed(integrationEvent.EventId))
        {
            var by = string.IsNullOrWhiteSpace(integrationEvent.QuarantinedBy) ? "the system" : integrationEvent.QuarantinedBy;
            var subject = string.Format(
                CultureInfo.InvariantCulture,
                "Line {0} placed under quarantine by {1}",
                integrationEvent.LineId,
                by);

            _board.PushAlert(
                integrationEvent.Tenant,
                new AlertTile(nameof(QualityLineQuarantined), AlertLevels.Warning, subject, integrationEvent.OccurredOnUtc));
        }

        return Task.CompletedTask;
    }
}
