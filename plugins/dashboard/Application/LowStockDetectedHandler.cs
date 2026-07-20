using System.Globalization;
using FactoryOS.Contracts.Events;
using FactoryOS.Plugins.Dashboard.Domain;

namespace FactoryOS.Plugins.Dashboard.Application;

/// <summary>Pushes a <see cref="LowStockDetected"/> onto the board as a warning alert.</summary>
public sealed class LowStockDetectedHandler : IEventHandler<LowStockDetected>
{
    private readonly IOperationsBoard _board;
    private readonly IProcessedEventLog _processed;

    /// <summary>Initializes a new instance of the <see cref="LowStockDetectedHandler"/> class.</summary>
    /// <param name="board">The operations read-model.</param>
    /// <param name="processed">The processed-event log for idempotency.</param>
    public LowStockDetectedHandler(IOperationsBoard board, IProcessedEventLog processed)
    {
        ArgumentNullException.ThrowIfNull(board);
        ArgumentNullException.ThrowIfNull(processed);
        _board = board;
        _processed = processed;
    }

    /// <inheritdoc />
    public Task HandleAsync(LowStockDetected integrationEvent, EventContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        ArgumentNullException.ThrowIfNull(context);

        if (_processed.TryMarkProcessed(integrationEvent.EventId))
        {
            var subject = string.Format(
                CultureInfo.InvariantCulture,
                "Low stock {0} at {1} ({2} <= {3})",
                integrationEvent.Sku,
                integrationEvent.WarehouseId,
                integrationEvent.OnHand,
                integrationEvent.ReorderPoint);

            _board.PushAlert(
                integrationEvent.Tenant,
                new AlertTile(nameof(LowStockDetected), AlertLevels.Warning, subject, integrationEvent.OccurredAt));
        }

        return Task.CompletedTask;
    }
}
