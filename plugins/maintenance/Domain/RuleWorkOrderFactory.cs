using System.Globalization;
using FactoryOS.Contracts.Events;
using FactoryOS.Contracts.StandardModel;

namespace FactoryOS.Plugins.Maintenance.Domain;

/// <summary>
/// Builds the corrective <see cref="WorkOrder"/> raised in response to a fired rule. Pure and deterministic: the
/// work-order number is derived from the trigger event's own id, so re-processing the same trigger yields the same
/// number — the basis for idempotent, duplicate-free creation — while two different rules firing on one reading
/// are distinct triggers and yield distinct orders.
/// </summary>
public static class RuleWorkOrderFactory
{
    /// <summary>Creates the work order for a fired rule under the given options.</summary>
    /// <param name="trigger">The triggering rule event.</param>
    /// <param name="options">The numbering and scheduling options.</param>
    /// <returns>An <c>Open</c> work order targeting the rule's meter, due per the options.</returns>
    public static WorkOrder FromTrigger(RuleTriggered trigger, MaintenanceOptions options)
    {
        ArgumentNullException.ThrowIfNull(trigger);
        ArgumentNullException.ThrowIfNull(options);

        var number = $"{options.RuleWorkOrderPrefix}-{trigger.EventId:N}"[..(options.RuleWorkOrderPrefix.Length + 9)]
            .ToUpperInvariant();

        var title = string.Format(
            CultureInfo.InvariantCulture,
            "Rule {0} fired on {1} ({2}): {3:0.#} {4} {5:0.#}",
            trigger.RuleId,
            trigger.MeterId,
            trigger.Metric,
            trigger.Value,
            trigger.Operator,
            trigger.Threshold);

        return new WorkOrder
        {
            Tenant = trigger.Tenant,
            Number = number,
            Title = title,
            Status = "Open",
            AssetCode = trigger.MeterId,
            DueAt = trigger.TriggeredAt.AddHours(options.RuleWorkOrderDueInHours),
        };
    }
}
