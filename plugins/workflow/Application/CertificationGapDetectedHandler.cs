using System.Globalization;
using FactoryOS.Contracts.Events;
using FactoryOS.Plugins.Workflow.Domain;

namespace FactoryOS.Plugins.Workflow.Application;

/// <summary>Normalizes a <see cref="CertificationGapDetected"/> into a workflow signal and processes it.</summary>
public sealed class CertificationGapDetectedHandler : IEventHandler<CertificationGapDetected>
{
    private readonly WorkflowEngine _engine;

    /// <summary>Initializes a new instance of the <see cref="CertificationGapDetectedHandler"/> class.</summary>
    /// <param name="engine">The workflow engine.</param>
    public CertificationGapDetectedHandler(WorkflowEngine engine)
    {
        ArgumentNullException.ThrowIfNull(engine);
        _engine = engine;
    }

    /// <inheritdoc />
    public Task HandleAsync(CertificationGapDetected integrationEvent, EventContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        ArgumentNullException.ThrowIfNull(context);

        var subject = string.Format(
            CultureInfo.InvariantCulture,
            "Certification gap ({0}) for {1} on shift {2}",
            integrationEvent.Reason,
            integrationEvent.WorkerId,
            integrationEvent.ShiftId);

        return _engine.ProcessAsync(
            new WorkflowSignal(
                integrationEvent.Tenant,
                nameof(CertificationGapDetected),
                subject,
                integrationEvent.ShiftStart,
                integrationEvent.EventId),
            cancellationToken);
    }
}
