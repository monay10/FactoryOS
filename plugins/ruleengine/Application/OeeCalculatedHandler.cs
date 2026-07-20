using FactoryOS.Contracts.Events;
using FactoryOS.Plugins.RuleEngine.Domain;

namespace FactoryOS.Plugins.RuleEngine.Application;

/// <summary>
/// The Rule Engine's second signal source: it evaluates the same declarative rules against computed
/// <see cref="OeeCalculated"/> facts, treating OEE as the Standard Model metric <c>Oee</c> (a fraction in
/// <c>[0, 1]</c>) and the machine as the "meter". A rule such as <c>Oee LessThan 0.6 → RaiseMaintenanceAlert</c>
/// thus fires on OEE degradation exactly as a temperature rule fires on an over-temp reading — one rule vocabulary
/// over two signal streams. It emits a <see cref="RuleTriggered"/> once per (rule, OEE event) pair, references the
/// shared events and the pure evaluator only, and never the OEE module or any consuming module.
/// </summary>
public sealed class OeeCalculatedHandler : IEventHandler<OeeCalculated>
{
    /// <summary>The Standard Model metric name computed OEE is matched against, case-insensitively.</summary>
    public const string OeeMetric = "Oee";

    private readonly IEventBus _bus;
    private readonly RuleEngineOptions _options;
    private readonly IRuleFiringLog _firingLog;

    /// <summary>Initializes a new instance of the <see cref="OeeCalculatedHandler"/> class.</summary>
    /// <param name="bus">The event bus to announce matched rules on.</param>
    /// <param name="options">The module options carrying the rules.</param>
    /// <param name="firingLog">The per-(rule, event) firing log for idempotency.</param>
    public OeeCalculatedHandler(IEventBus bus, RuleEngineOptions options, IRuleFiringLog firingLog)
    {
        ArgumentNullException.ThrowIfNull(bus);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(firingLog);
        _bus = bus;
        _options = options;
        _firingLog = firingLog;
    }

    /// <inheritdoc />
    public async Task HandleAsync(OeeCalculated integrationEvent, EventContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        ArgumentNullException.ThrowIfNull(context);

        foreach (var rule in _options.Rules)
        {
            if (!string.Equals(rule.Metric, OeeMetric, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!RuleEvaluator.Matches(rule.Operator, integrationEvent.Oee, rule.Threshold))
            {
                continue;
            }

            if (!_firingLog.TryMarkFired(rule.Id, integrationEvent.EventId))
            {
                continue;
            }

            await _bus.PublishAsync(
                new RuleTriggered
                {
                    Tenant = integrationEvent.Tenant,
                    RuleId = rule.Id,
                    Metric = OeeMetric,
                    MeterId = integrationEvent.MachineId,
                    Value = integrationEvent.Oee,
                    Operator = rule.Operator.ToString(),
                    Threshold = rule.Threshold,
                    Action = rule.Action,
                    TriggeredAt = integrationEvent.PeriodEnd,
                    SourceEventId = integrationEvent.EventId,
                },
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }
    }
}
