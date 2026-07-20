using FactoryOS.Contracts.Events;
using FactoryOS.Plugins.Hr.Domain;

namespace FactoryOS.Plugins.Hr.Application;

/// <summary>
/// The HR module's consumer of <see cref="ShiftStaffed"/>. When a shift requires a certification, it checks the
/// staffed worker's recorded certification against the shift start and, if the worker lacks a valid one, raises a
/// <see cref="CertificationGapDetected"/>. It references no other module — only the shared event vocabulary.
/// Delivery being at-least-once, the handler deduplicates by event id so the same staffing is checked once.
/// </summary>
public sealed class ShiftStaffedHandler : IEventHandler<ShiftStaffed>
{
    private readonly IEventBus _bus;
    private readonly ICertificationRegistry _registry;
    private readonly IProcessedEventLog _processed;
    private readonly HrOptions _options;

    /// <summary>Initializes a new instance of the <see cref="ShiftStaffedHandler"/> class.</summary>
    /// <param name="bus">The event bus to publish HR events on.</param>
    /// <param name="registry">The certification registry.</param>
    /// <param name="processed">The processed-event log for idempotency.</param>
    /// <param name="options">The module options.</param>
    public ShiftStaffedHandler(
        IEventBus bus,
        ICertificationRegistry registry,
        IProcessedEventLog processed,
        HrOptions options)
    {
        ArgumentNullException.ThrowIfNull(bus);
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(processed);
        ArgumentNullException.ThrowIfNull(options);
        _bus = bus;
        _registry = registry;
        _processed = processed;
        _options = options;
    }

    /// <inheritdoc />
    public async Task HandleAsync(
        ShiftStaffed integrationEvent,
        EventContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        ArgumentNullException.ThrowIfNull(context);

        if (string.IsNullOrWhiteSpace(integrationEvent.RequiredCertification))
        {
            return; // the shift requires no certification
        }

        // At-least-once delivery: check each staffing once.
        if (!_processed.TryMarkProcessed(integrationEvent.EventId))
        {
            return;
        }

        var key = new WorkerKey(integrationEvent.Tenant, integrationEvent.WorkerId);
        var expiry = _registry.ExpiryOf(key, integrationEvent.RequiredCertification);

        var gap = CertificationEvaluator.Evaluate(expiry, integrationEvent.ShiftStart, _options);
        if (!gap.IsGap)
        {
            return;
        }

        await _bus.PublishAsync(
            new CertificationGapDetected
            {
                Tenant = integrationEvent.Tenant,
                ShiftId = integrationEvent.ShiftId,
                WorkerId = integrationEvent.WorkerId,
                RequiredCertification = integrationEvent.RequiredCertification,
                Reason = gap.Reason,
                ExpiresAt = expiry,
                ShiftStart = integrationEvent.ShiftStart,
                SourceEventId = integrationEvent.EventId,
            },
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}
