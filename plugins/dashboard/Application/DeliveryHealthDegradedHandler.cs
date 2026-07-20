using System.Globalization;
using FactoryOS.Contracts.Events;
using FactoryOS.Plugins.Dashboard.Domain;

namespace FactoryOS.Plugins.Dashboard.Application;

/// <summary>Pushes a <see cref="DeliveryHealthDegraded"/> onto the board as a warning alert.</summary>
public sealed class DeliveryHealthDegradedHandler : IEventHandler<DeliveryHealthDegraded>
{
    private readonly IOperationsBoard _board;
    private readonly IProcessedEventLog _processed;

    /// <summary>Initializes a new instance of the <see cref="DeliveryHealthDegradedHandler"/> class.</summary>
    /// <param name="board">The operations read-model.</param>
    /// <param name="processed">The processed-event log for idempotency.</param>
    public DeliveryHealthDegradedHandler(IOperationsBoard board, IProcessedEventLog processed)
    {
        ArgumentNullException.ThrowIfNull(board);
        ArgumentNullException.ThrowIfNull(processed);
        _board = board;
        _processed = processed;
    }

    /// <inheritdoc />
    public Task HandleAsync(DeliveryHealthDegraded integrationEvent, EventContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        ArgumentNullException.ThrowIfNull(context);

        if (_processed.TryMarkProcessed(integrationEvent.EventId))
        {
            var subject = string.Format(
                CultureInfo.InvariantCulture,
                "Delivery degraded on {0}: {1} consecutive failures ({2} of {3} attempts failed)",
                integrationEvent.Transport,
                integrationEvent.ConsecutiveFailures,
                integrationEvent.Failed,
                integrationEvent.Attempts);

            _board.PushAlert(
                integrationEvent.Tenant,
                new AlertTile(nameof(DeliveryHealthDegraded), AlertLevels.Warning, subject, integrationEvent.DetectedAt));
        }

        return Task.CompletedTask;
    }
}
