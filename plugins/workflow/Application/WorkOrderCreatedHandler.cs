using System.Globalization;
using FactoryOS.Contracts.Events;
using FactoryOS.Plugins.Workflow.Domain;

namespace FactoryOS.Plugins.Workflow.Application;

/// <summary>Normalizes a <see cref="WorkOrderCreated"/> into a workflow signal and processes it, so a raised work
/// order can be escalated or notified per the tenant's rules without Workflow referencing the Maintenance module.</summary>
public sealed class WorkOrderCreatedHandler : IEventHandler<WorkOrderCreated>
{
    private readonly WorkflowEngine _engine;

    /// <summary>Initializes a new instance of the <see cref="WorkOrderCreatedHandler"/> class.</summary>
    /// <param name="engine">The workflow engine.</param>
    public WorkOrderCreatedHandler(WorkflowEngine engine)
    {
        ArgumentNullException.ThrowIfNull(engine);
        _engine = engine;
    }

    /// <inheritdoc />
    public Task HandleAsync(WorkOrderCreated integrationEvent, EventContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        ArgumentNullException.ThrowIfNull(context);

        var workOrder = integrationEvent.WorkOrder;
        var subject = string.Format(
            CultureInfo.InvariantCulture,
            "Work order {0} raised: {1} ({2})",
            workOrder.Number,
            workOrder.Title,
            integrationEvent.Reason);

        return _engine.ProcessAsync(
            new WorkflowSignal(
                workOrder.Tenant,
                nameof(WorkOrderCreated),
                subject,
                integrationEvent.OccurredOnUtc,
                integrationEvent.EventId),
            cancellationToken);
    }
}
