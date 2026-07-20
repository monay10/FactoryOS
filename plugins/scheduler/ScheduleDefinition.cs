namespace FactoryOS.Plugins.Scheduler;

/// <summary>
/// One configured schedule: a stable id, the action it requests, and its fixed cadence in seconds. Supplied as
/// data — adding, retiming or removing a schedule is a config change, never code.
/// </summary>
public sealed record ScheduleDefinition
{
    /// <summary>The schedule's stable id (for example <c>daily-oee-report</c>).</summary>
    public required string Id { get; init; }

    /// <summary>The action the schedule requests when due.</summary>
    public required string Action { get; init; }

    /// <summary>The fixed cadence in seconds; a value of zero or less fires on every pulse.</summary>
    public int EverySeconds { get; init; }
}
