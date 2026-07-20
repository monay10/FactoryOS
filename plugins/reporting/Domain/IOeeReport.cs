namespace FactoryOS.Plugins.Reporting.Domain;

/// <summary>
/// The tenant-scoped OEE reporting read-model: the write side (fed by the OEE event handler) folds each reading
/// into its machine's daily bucket; the read side (a report) asks for a machine's daily history. A CQRS read
/// model kept current purely by consuming the event bus.
/// </summary>
public interface IOeeReport
{
    /// <summary>Folds one OEE reading into a machine's day bucket for a tenant.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <param name="machineId">The machine.</param>
    /// <param name="day">The UTC calendar day the reading belongs to.</param>
    /// <param name="oee">The OEE reading, a fraction in <c>[0, 1]</c>.</param>
    void Record(string tenant, string machineId, DateOnly day, decimal oee);

    /// <summary>Returns a machine's daily OEE stats for a tenant, newest day first.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <param name="machineId">The machine.</param>
    /// <returns>The daily stats, or an empty list if none.</returns>
    IReadOnlyList<OeeDailyStat> ForMachine(string tenant, string machineId);

    /// <summary>Returns the ids of all machines a tenant has OEE history for, ordered by id.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <returns>The machine ids, or an empty list if none.</returns>
    IReadOnlyList<string> Machines(string tenant);
}
