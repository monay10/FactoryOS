using System.Globalization;
using FactoryOS.Contracts.Events;
using FactoryOS.Plugins.Workflow.Domain;

namespace FactoryOS.Plugins.Workflow.Application;

/// <summary>Normalizes a <see cref="LowStockDetected"/> into a workflow signal and processes it.</summary>
public sealed class LowStockDetectedHandler : IEventHandler<LowStockDetected>
{
    private readonly WorkflowEngine _engine;

    /// <summary>Initializes a new instance of the <see cref="LowStockDetectedHandler"/> class.</summary>
    /// <param name="engine">The workflow engine.</param>
    public LowStockDetectedHandler(WorkflowEngine engine)
    {
        ArgumentNullException.ThrowIfNull(engine);
        _engine = engine;
    }

    /// <inheritdoc />
    public Task HandleAsync(LowStockDetected integrationEvent, EventContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        ArgumentNullException.ThrowIfNull(context);

        var subject = string.Format(
            CultureInfo.InvariantCulture,
            "Low stock on {0} at {1} ({2}/{3})",
            integrationEvent.Sku,
            integrationEvent.WarehouseId,
            integrationEvent.OnHand,
            integrationEvent.ReorderPoint);

        return _engine.ProcessAsync(
            new WorkflowSignal(
                integrationEvent.Tenant,
                nameof(LowStockDetected),
                subject,
                integrationEvent.OccurredAt,
                integrationEvent.EventId),
            cancellationToken);
    }
}
