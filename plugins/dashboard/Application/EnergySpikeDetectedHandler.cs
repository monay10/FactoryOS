using System.Globalization;
using FactoryOS.Contracts.Events;
using FactoryOS.Plugins.Dashboard.Domain;

namespace FactoryOS.Plugins.Dashboard.Application;

/// <summary>Pushes an <see cref="EnergySpikeDetected"/> onto the board as a warning alert.</summary>
public sealed class EnergySpikeDetectedHandler : IEventHandler<EnergySpikeDetected>
{
    private readonly IOperationsBoard _board;
    private readonly IProcessedEventLog _processed;

    /// <summary>Initializes a new instance of the <see cref="EnergySpikeDetectedHandler"/> class.</summary>
    /// <param name="board">The operations read-model.</param>
    /// <param name="processed">The processed-event log for idempotency.</param>
    public EnergySpikeDetectedHandler(IOperationsBoard board, IProcessedEventLog processed)
    {
        ArgumentNullException.ThrowIfNull(board);
        ArgumentNullException.ThrowIfNull(processed);
        _board = board;
        _processed = processed;
    }

    /// <inheritdoc />
    public Task HandleAsync(EnergySpikeDetected integrationEvent, EventContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        ArgumentNullException.ThrowIfNull(context);

        if (_processed.TryMarkProcessed(integrationEvent.EventId))
        {
            var subject = string.Format(
                CultureInfo.InvariantCulture,
                "Energy spike on {0}: {1} {2:0.##}{3} is {4:0.#}% over baseline {5:0.##}{3}",
                integrationEvent.MeterId,
                integrationEvent.Metric,
                integrationEvent.Value,
                integrationEvent.Unit,
                integrationEvent.DeltaPercent,
                integrationEvent.Baseline);

            _board.PushAlert(
                integrationEvent.Tenant,
                new AlertTile(nameof(EnergySpikeDetected), AlertLevels.Warning, subject, integrationEvent.ReadingAt));
        }

        return Task.CompletedTask;
    }
}
