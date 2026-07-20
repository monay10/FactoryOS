namespace FactoryOS.Plugins.Dashboard.Domain;

/// <summary>
/// A single machine's latest OEE, as rendered on the board. One tile per machine; a newer reading replaces the
/// older one (last-write-wins), so the board always shows the current state, never a history.
/// </summary>
/// <param name="MachineId">The machine the tile is for.</param>
/// <param name="Oee">Overall Equipment Effectiveness, a fraction in <c>[0, 1]</c>.</param>
/// <param name="MeetsTarget">Whether the latest OEE met the machine's configured target.</param>
/// <param name="AsOf">The end of the period this OEE was computed for.</param>
public readonly record struct OeeTile(string MachineId, decimal Oee, bool MeetsTarget, DateTimeOffset AsOf);
