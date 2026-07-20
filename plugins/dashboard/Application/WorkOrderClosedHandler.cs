using System.Globalization;
using FactoryOS.Contracts.Events;
using FactoryOS.Plugins.Dashboard.Domain;

namespace FactoryOS.Plugins.Dashboard.Application;

/// <summary>Pushes a <see cref="WorkOrderClosed"/> onto the board as an informational alert — a positive signal that
/// maintenance work has completed, balancing the warnings that raised it. Consumes the shared event, never referencing
/// the Maintenance module, and is idempotent by event id so redelivery does not double the tile.</summary>
public sealed class WorkOrderClosedHandler : IEventHandler<WorkOrderClosed>
{
    private readonly IOperationsBoard _board;
    private readonly IProcessedEventLog _processed;

    /// <summary>Initializes a new instance of the <see cref="WorkOrderClosedHandler"/> class.</summary>
    /// <param name="board">The operations read-model.</param>
    /// <param name="processed">The processed-event log for idempotency.</param>
    public WorkOrderClosedHandler(IOperationsBoard board, IProcessedEventLog processed)
    {
        ArgumentNullException.ThrowIfNull(board);
        ArgumentNullException.ThrowIfNull(processed);
        _board = board;
        _processed = processed;
    }

    /// <inheritdoc />
    public Task HandleAsync(WorkOrderClosed integrationEvent, EventContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        ArgumentNullException.ThrowIfNull(context);

        if (_processed.TryMarkProcessed(integrationEvent.EventId))
        {
            var workOrder = integrationEvent.WorkOrder;
            var subject = string.Format(
                CultureInfo.InvariantCulture,
                "Work order {0} closed on {1}: {2}",
                workOrder.Number,
                workOrder.AssetCode,
                workOrder.Title);

            _board.PushAlert(
                workOrder.Tenant,
                new AlertTile(nameof(WorkOrderClosed), AlertLevels.Info, subject, integrationEvent.OccurredOnUtc));
        }

        return Task.CompletedTask;
    }
}
