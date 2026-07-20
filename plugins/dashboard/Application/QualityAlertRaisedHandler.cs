using System.Globalization;
using FactoryOS.Contracts.Events;
using FactoryOS.Plugins.Dashboard.Domain;

namespace FactoryOS.Plugins.Dashboard.Application;

/// <summary>Pushes a <see cref="QualityAlertRaised"/> onto the board as a warning alert.</summary>
public sealed class QualityAlertRaisedHandler : IEventHandler<QualityAlertRaised>
{
    private readonly IOperationsBoard _board;
    private readonly IProcessedEventLog _processed;

    /// <summary>Initializes a new instance of the <see cref="QualityAlertRaisedHandler"/> class.</summary>
    /// <param name="board">The operations read-model.</param>
    /// <param name="processed">The processed-event log for idempotency.</param>
    public QualityAlertRaisedHandler(IOperationsBoard board, IProcessedEventLog processed)
    {
        ArgumentNullException.ThrowIfNull(board);
        ArgumentNullException.ThrowIfNull(processed);
        _board = board;
        _processed = processed;
    }

    /// <inheritdoc />
    public Task HandleAsync(QualityAlertRaised integrationEvent, EventContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        ArgumentNullException.ThrowIfNull(context);

        if (_processed.TryMarkProcessed(integrationEvent.EventId))
        {
            var subject = string.Format(
                CultureInfo.InvariantCulture,
                "Quality defect rate {0:P1} on {1}/{2}",
                integrationEvent.DefectRate,
                integrationEvent.LineId,
                integrationEvent.ProductId);

            _board.PushAlert(
                integrationEvent.Tenant,
                new AlertTile(nameof(QualityAlertRaised), AlertLevels.Warning, subject, integrationEvent.InspectedAt));
        }

        return Task.CompletedTask;
    }
}
