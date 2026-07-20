using FactoryOS.Contracts.Events;
using FactoryOS.Plugins.Dashboard.Domain;

namespace FactoryOS.Plugins.Dashboard.Application;

/// <summary>Folds an <see cref="OeeCalculated"/> fact into the board as the machine's latest OEE tile.</summary>
public sealed class OeeCalculatedHandler : IEventHandler<OeeCalculated>
{
    private readonly IOperationsBoard _board;
    private readonly IProcessedEventLog _processed;

    /// <summary>Initializes a new instance of the <see cref="OeeCalculatedHandler"/> class.</summary>
    /// <param name="board">The operations read-model.</param>
    /// <param name="processed">The processed-event log for idempotency.</param>
    public OeeCalculatedHandler(IOperationsBoard board, IProcessedEventLog processed)
    {
        ArgumentNullException.ThrowIfNull(board);
        ArgumentNullException.ThrowIfNull(processed);
        _board = board;
        _processed = processed;
    }

    /// <inheritdoc />
    public Task HandleAsync(OeeCalculated integrationEvent, EventContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        ArgumentNullException.ThrowIfNull(context);

        if (_processed.TryMarkProcessed(integrationEvent.EventId))
        {
            _board.RecordOee(
                integrationEvent.Tenant,
                new OeeTile(
                    integrationEvent.MachineId,
                    integrationEvent.Oee,
                    integrationEvent.MeetsTarget,
                    integrationEvent.PeriodEnd));
        }

        return Task.CompletedTask;
    }
}
