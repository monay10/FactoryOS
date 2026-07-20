using System.Globalization;
using FactoryOS.Contracts.Events;
using FactoryOS.Contracts.StandardModel;

namespace FactoryOS.Plugins.Maintenance.Domain;

/// <summary>
/// Builds the corrective <see cref="WorkOrder"/> raised in response to an energy spike. Pure and
/// deterministic: the work-order number is derived from the triggering event's id, so re-processing the same
/// spike yields the same number — the basis for idempotent, duplicate-free work-order creation.
/// </summary>
public static class SpikeWorkOrderFactory
{
    /// <summary>Creates the work order for a spike under the given options.</summary>
    /// <param name="spike">The triggering energy-spike event.</param>
    /// <param name="options">The numbering and scheduling options.</param>
    /// <returns>An <c>Open</c> work order targeting the spiking meter, due per the options.</returns>
    public static WorkOrder FromSpike(EnergySpikeDetected spike, MaintenanceOptions options)
    {
        ArgumentNullException.ThrowIfNull(spike);
        ArgumentNullException.ThrowIfNull(options);

        var number = $"{options.SpikeWorkOrderPrefix}-{spike.EventId:N}"[..(options.SpikeWorkOrderPrefix.Length + 9)]
            .ToUpperInvariant();

        var title = string.Format(
            CultureInfo.InvariantCulture,
            "Investigate energy spike on {0} ({1}): {2:0.#}% above baseline",
            spike.MeterId,
            spike.Metric,
            spike.DeltaPercent);

        return new WorkOrder
        {
            Tenant = spike.Tenant,
            Number = number,
            Title = title,
            Status = "Open",
            AssetCode = spike.MeterId,
            DueAt = spike.ReadingAt.AddHours(options.SpikeWorkOrderDueInHours),
        };
    }
}
