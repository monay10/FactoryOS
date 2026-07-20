using FactoryOS.Contracts.Events;
using FactoryOS.Plugins.Quality.Domain;

namespace FactoryOS.Plugins.Quality.Application;

/// <summary>
/// The Quality module's consumer of <see cref="QualityInspectionRecorded"/>. It folds each inspection into a
/// rolling defect-rate window and, once enough units have been inspected, raises a <see cref="QualityAlertRaised"/>
/// when the rate breaches the threshold. It references no other module — only the shared event vocabulary.
/// Delivery is at-least-once, so the handler deduplicates by event id before folding, keeping the consumer
/// idempotent; an empty inspection is a no-op.
/// </summary>
public sealed class QualityInspectionRecordedHandler : IEventHandler<QualityInspectionRecorded>
{
    private readonly IEventBus _bus;
    private readonly IDefectRateWindowStore _windows;
    private readonly IProcessedEventLog _processed;
    private readonly QualityOptions _options;

    /// <summary>Initializes a new instance of the <see cref="QualityInspectionRecordedHandler"/> class.</summary>
    /// <param name="bus">The event bus to publish quality events on.</param>
    /// <param name="windows">The rolling defect-rate window store.</param>
    /// <param name="processed">The processed-event log for idempotency.</param>
    /// <param name="options">The module options.</param>
    public QualityInspectionRecordedHandler(
        IEventBus bus,
        IDefectRateWindowStore windows,
        IProcessedEventLog processed,
        QualityOptions options)
    {
        ArgumentNullException.ThrowIfNull(bus);
        ArgumentNullException.ThrowIfNull(windows);
        ArgumentNullException.ThrowIfNull(processed);
        ArgumentNullException.ThrowIfNull(options);
        _bus = bus;
        _windows = windows;
        _processed = processed;
        _options = options;
    }

    /// <inheritdoc />
    public async Task HandleAsync(
        QualityInspectionRecorded integrationEvent,
        EventContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        ArgumentNullException.ThrowIfNull(context);

        // At-least-once delivery: skip an inspection already folded into the window.
        if (!_processed.TryMarkProcessed(integrationEvent.EventId))
        {
            return;
        }

        if (integrationEvent.InspectedUnits <= 0)
        {
            return; // nothing inspected → no signal to fold
        }

        var key = new QualityLineKey(integrationEvent.Tenant, integrationEvent.LineId, integrationEvent.ProductId);
        var window = _windows.Fold(key, integrationEvent.InspectedUnits, integrationEvent.DefectiveUnits);

        var evaluation = DefectRateEvaluator.Evaluate(window, _options);
        if (!evaluation.IsBreach)
        {
            return;
        }

        await _bus.PublishAsync(
            new QualityAlertRaised
            {
                Tenant = integrationEvent.Tenant,
                LineId = integrationEvent.LineId,
                ProductId = integrationEvent.ProductId,
                DefectRate = evaluation.DefectRate,
                Threshold = _options.DefectRateThreshold,
                WindowInspectedUnits = evaluation.Window.InspectedUnits,
                WindowDefectiveUnits = evaluation.Window.DefectiveUnits,
                InspectedAt = integrationEvent.InspectedAt,
                SourceEventId = integrationEvent.EventId,
            },
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}
