namespace FactoryOS.Contracts.Events;

/// <summary>
/// The shared fact that a configured schedule has come due — its fixed interval elapsed since its last run.
/// Connectors (to pull data), reporting, maintenance and AI agents consume it to do periodic work, without
/// referencing the Scheduler module. It is the normalized "it is time to do X" signal.
/// </summary>
public sealed record ScheduledTaskDue : IntegrationEvent
{
    /// <summary>The tenant the schedule belongs to.</summary>
    public required string Tenant { get; init; }

    /// <summary>The schedule's stable id (for example <c>daily-oee-report</c>).</summary>
    public required string ScheduleId { get; init; }

    /// <summary>The action the schedule requests (for example <c>PullErpStock</c> or <c>GenerateReport</c>).</summary>
    public required string Action { get; init; }

    /// <summary>The schedule's fixed cadence in seconds.</summary>
    public int EverySeconds { get; init; }

    /// <summary>The instant the schedule became due (the triggering pulse's instant).</summary>
    public DateTimeOffset DueAt { get; init; }
}
