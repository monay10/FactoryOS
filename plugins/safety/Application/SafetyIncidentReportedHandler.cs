using FactoryOS.Contracts.Events;
using FactoryOS.Plugins.Safety.Domain;

namespace FactoryOS.Plugins.Safety.Application;

/// <summary>
/// The Safety module's consumer of <see cref="SafetyIncidentReported"/>. It folds each incident into the site's
/// rolling window and, when the incident is severe enough or incidents have accumulated past the frequency
/// threshold, recommends a <see cref="SafetyStandDownTriggered"/>. It references no other module — only the
/// shared event vocabulary. Delivery being at-least-once, the handler deduplicates by event id before folding.
/// </summary>
public sealed class SafetyIncidentReportedHandler : IEventHandler<SafetyIncidentReported>
{
    private readonly IEventBus _bus;
    private readonly IIncidentWindowStore _windows;
    private readonly IProcessedEventLog _processed;
    private readonly SafetyOptions _options;

    /// <summary>Initializes a new instance of the <see cref="SafetyIncidentReportedHandler"/> class.</summary>
    /// <param name="bus">The event bus to publish safety events on.</param>
    /// <param name="windows">The rolling incident-window store.</param>
    /// <param name="processed">The processed-event log for idempotency.</param>
    /// <param name="options">The module options.</param>
    public SafetyIncidentReportedHandler(
        IEventBus bus,
        IIncidentWindowStore windows,
        IProcessedEventLog processed,
        SafetyOptions options)
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
        SafetyIncidentReported integrationEvent,
        EventContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        ArgumentNullException.ThrowIfNull(context);

        // At-least-once delivery: skip an incident already folded into the window.
        if (!_processed.TryMarkProcessed(integrationEvent.EventId))
        {
            return;
        }

        var count = _windows.Fold(new SafetySiteKey(integrationEvent.Tenant, integrationEvent.SiteId));

        var decision = SafetyEvaluator.Evaluate(integrationEvent.Severity, count, _options);
        if (!decision.StandDown)
        {
            return;
        }

        await _bus.PublishAsync(
            new SafetyStandDownTriggered
            {
                Tenant = integrationEvent.Tenant,
                SiteId = integrationEvent.SiteId,
                Reason = decision.Reason,
                TriggerSeverity = integrationEvent.Severity,
                WindowIncidentCount = count,
                OccurredAt = integrationEvent.OccurredAt,
                SourceEventId = integrationEvent.EventId,
            },
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}
