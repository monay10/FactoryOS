using System.Globalization;
using FactoryOS.Contracts.Events;
using FactoryOS.Plugins.Dashboard.Domain;

namespace FactoryOS.Plugins.Dashboard.Application;

/// <summary>Pushes a <see cref="SafetyStandDownTriggered"/> onto the board as a critical alert.</summary>
public sealed class SafetyStandDownTriggeredHandler : IEventHandler<SafetyStandDownTriggered>
{
    private readonly IOperationsBoard _board;
    private readonly IProcessedEventLog _processed;

    /// <summary>Initializes a new instance of the <see cref="SafetyStandDownTriggeredHandler"/> class.</summary>
    /// <param name="board">The operations read-model.</param>
    /// <param name="processed">The processed-event log for idempotency.</param>
    public SafetyStandDownTriggeredHandler(IOperationsBoard board, IProcessedEventLog processed)
    {
        ArgumentNullException.ThrowIfNull(board);
        ArgumentNullException.ThrowIfNull(processed);
        _board = board;
        _processed = processed;
    }

    /// <inheritdoc />
    public Task HandleAsync(SafetyStandDownTriggered integrationEvent, EventContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        ArgumentNullException.ThrowIfNull(context);

        if (_processed.TryMarkProcessed(integrationEvent.EventId))
        {
            var subject = string.Format(
                CultureInfo.InvariantCulture,
                "Safety stand-down at {0} ({1})",
                integrationEvent.SiteId,
                integrationEvent.Reason);

            _board.PushAlert(
                integrationEvent.Tenant,
                new AlertTile(nameof(SafetyStandDownTriggered), AlertLevels.Critical, subject, integrationEvent.OccurredAt));
        }

        return Task.CompletedTask;
    }
}
