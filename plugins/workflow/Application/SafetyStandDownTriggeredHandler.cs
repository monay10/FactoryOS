using System.Globalization;
using FactoryOS.Contracts.Events;
using FactoryOS.Plugins.Workflow.Domain;

namespace FactoryOS.Plugins.Workflow.Application;

/// <summary>Normalizes a <see cref="SafetyStandDownTriggered"/> into a workflow signal and processes it.</summary>
public sealed class SafetyStandDownTriggeredHandler : IEventHandler<SafetyStandDownTriggered>
{
    private readonly WorkflowEngine _engine;

    /// <summary>Initializes a new instance of the <see cref="SafetyStandDownTriggeredHandler"/> class.</summary>
    /// <param name="engine">The workflow engine.</param>
    public SafetyStandDownTriggeredHandler(WorkflowEngine engine)
    {
        ArgumentNullException.ThrowIfNull(engine);
        _engine = engine;
    }

    /// <inheritdoc />
    public Task HandleAsync(SafetyStandDownTriggered integrationEvent, EventContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        ArgumentNullException.ThrowIfNull(context);

        var subject = string.Format(
            CultureInfo.InvariantCulture,
            "Safety stand-down at {0} ({1})",
            integrationEvent.SiteId,
            integrationEvent.Reason);

        return _engine.ProcessAsync(
            new WorkflowSignal(
                integrationEvent.Tenant,
                nameof(SafetyStandDownTriggered),
                subject,
                integrationEvent.OccurredAt,
                integrationEvent.EventId),
            cancellationToken);
    }
}
