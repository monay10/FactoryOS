namespace FactoryOS.Plugins.Scheduler;

/// <summary>
/// Configuration for the Scheduler module. The set of schedules is data, never a customer branch: a factory
/// declares what recurring work it wants and how often, purely by configuration.
/// </summary>
public sealed record SchedulerOptions
{
    /// <summary>The configuration section this binds to.</summary>
    public const string SectionName = "Modules:Scheduler";

    /// <summary>The declarative schedules to evaluate on each pulse.</summary>
    public IReadOnlyList<ScheduleDefinition> Schedules { get; init; } = [];
}
