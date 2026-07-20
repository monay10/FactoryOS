using System.Globalization;
using FactoryOS.Contracts.Events;

namespace FactoryOS.Agents.Knowledge.Application;

/// <summary>Narrates a <see cref="CertificationGapDetected"/> into a knowledge document and ingests it, so the Company
/// Brain can later answer compliance questions ("which workers were staffed without a valid certification, and on
/// which shifts?"). References the shared event only, never the HR module.</summary>
public sealed class CertificationGapDetectedHandler : IEventHandler<CertificationGapDetected>
{
    private readonly KnowledgeIngestor _ingestor;

    /// <summary>Initializes a new instance of the <see cref="CertificationGapDetectedHandler"/> class.</summary>
    /// <param name="ingestor">The knowledge ingestor.</param>
    public CertificationGapDetectedHandler(KnowledgeIngestor ingestor)
    {
        ArgumentNullException.ThrowIfNull(ingestor);
        _ingestor = ingestor;
    }

    /// <inheritdoc />
    public Task HandleAsync(CertificationGapDetected integrationEvent, EventContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        ArgumentNullException.ThrowIfNull(context);

        var text = string.Format(
            CultureInfo.InvariantCulture,
            "On {0:u}, a certification gap was detected at tenant {1}: worker {2} was staffed on shift {3} without a valid {4} ({5}).",
            integrationEvent.ShiftStart,
            integrationEvent.Tenant,
            integrationEvent.WorkerId,
            integrationEvent.ShiftId,
            integrationEvent.RequiredCertification,
            integrationEvent.Reason);

        return _ingestor.IngestAsync(
            new KnowledgeSignal(
                integrationEvent.Tenant,
                $"activity/compliance/{integrationEvent.EventId:N}",
                text,
                integrationEvent.EventId),
            cancellationToken);
    }
}
