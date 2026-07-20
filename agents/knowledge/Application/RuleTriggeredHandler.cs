using System.Globalization;
using FactoryOS.Contracts.Events;

namespace FactoryOS.Agents.Knowledge.Application;

/// <summary>Narrates a <see cref="RuleTriggered"/> into a knowledge document and ingests it, so the Company Brain
/// can later answer questions about rules that fired. References the shared event only, never the Rule Engine.</summary>
public sealed class RuleTriggeredHandler : IEventHandler<RuleTriggered>
{
    private readonly KnowledgeIngestor _ingestor;

    /// <summary>Initializes a new instance of the <see cref="RuleTriggeredHandler"/> class.</summary>
    /// <param name="ingestor">The knowledge ingestor.</param>
    public RuleTriggeredHandler(KnowledgeIngestor ingestor)
    {
        ArgumentNullException.ThrowIfNull(ingestor);
        _ingestor = ingestor;
    }

    /// <inheritdoc />
    public Task HandleAsync(RuleTriggered integrationEvent, EventContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        ArgumentNullException.ThrowIfNull(context);

        var text = string.Format(
            CultureInfo.InvariantCulture,
            "On {0:u}, rule '{1}' fired for tenant {2}: meter {3} reported {4} = {5} ({6} threshold {7}), requesting action {8}.",
            integrationEvent.TriggeredAt,
            integrationEvent.RuleId,
            integrationEvent.Tenant,
            integrationEvent.MeterId,
            integrationEvent.Metric,
            integrationEvent.Value,
            integrationEvent.Operator,
            integrationEvent.Threshold,
            integrationEvent.Action);

        return _ingestor.IngestAsync(
            new KnowledgeSignal(
                integrationEvent.Tenant,
                $"activity/rule/{integrationEvent.EventId:N}",
                text,
                integrationEvent.EventId),
            cancellationToken);
    }
}
