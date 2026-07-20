using FactoryOS.Contracts.Events;
using FactoryOS.Plugins.Brain.Domain;

namespace FactoryOS.Plugins.Brain.Application;

/// <summary>Records a grounded answer as a Brain answer-log entry. Consumes the shared <see cref="BrainAnswered"/>
/// the Brain Query agent re-enters on the bus, never referencing that agent — the bus fans the answer out to whoever
/// cares, so a UI can read the Q&amp;A history over HTTP without touching the AI layer. Idempotent by construction:
/// the entry is keyed by the answer's source event id, so at-least-once redelivery is a no-op.</summary>
public sealed class BrainAnsweredHandler : IEventHandler<BrainAnswered>
{
    private readonly IBrainAnswerLog _log;

    /// <summary>Initializes a new instance of the <see cref="BrainAnsweredHandler"/> class.</summary>
    /// <param name="log">The Brain answer-log read-model.</param>
    public BrainAnsweredHandler(IBrainAnswerLog log)
    {
        ArgumentNullException.ThrowIfNull(log);
        _log = log;
    }

    /// <inheritdoc />
    public Task HandleAsync(BrainAnswered integrationEvent, EventContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        ArgumentNullException.ThrowIfNull(context);

        _log.Record(new BrainAnswerEntry(
            integrationEvent.Tenant,
            integrationEvent.Question,
            integrationEvent.Answer,
            integrationEvent.Model,
            integrationEvent.Citations,
            integrationEvent.AnsweredAt,
            integrationEvent.SourceEventId));

        return Task.CompletedTask;
    }
}
