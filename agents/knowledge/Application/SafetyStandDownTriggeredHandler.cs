using System.Globalization;
using FactoryOS.Contracts.Events;

namespace FactoryOS.Agents.Knowledge.Application;

/// <summary>Narrates a <see cref="SafetyStandDownTriggered"/> into a knowledge document and ingests it, so the
/// Company Brain can later answer questions about safety stand-downs. References the shared event only.</summary>
public sealed class SafetyStandDownTriggeredHandler : IEventHandler<SafetyStandDownTriggered>
{
    private readonly KnowledgeIngestor _ingestor;

    /// <summary>Initializes a new instance of the <see cref="SafetyStandDownTriggeredHandler"/> class.</summary>
    /// <param name="ingestor">The knowledge ingestor.</param>
    public SafetyStandDownTriggeredHandler(KnowledgeIngestor ingestor)
    {
        ArgumentNullException.ThrowIfNull(ingestor);
        _ingestor = ingestor;
    }

    /// <inheritdoc />
    public Task HandleAsync(SafetyStandDownTriggered integrationEvent, EventContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        ArgumentNullException.ThrowIfNull(context);

        var text = string.Format(
            CultureInfo.InvariantCulture,
            "On {0:u}, a safety stand-down was triggered at site {1} for tenant {2}. Reason: {3}; trigger severity {4}; {5} incidents in the recent window.",
            integrationEvent.OccurredAt,
            integrationEvent.SiteId,
            integrationEvent.Tenant,
            integrationEvent.Reason,
            integrationEvent.TriggerSeverity,
            integrationEvent.WindowIncidentCount);

        return _ingestor.IngestAsync(
            new KnowledgeSignal(
                integrationEvent.Tenant,
                $"activity/safety/{integrationEvent.EventId:N}",
                text,
                integrationEvent.EventId),
            cancellationToken);
    }
}
