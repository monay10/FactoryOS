namespace FactoryOS.Plugins.Workflow.SLA.Domain;

/// <summary>
/// One phase of a staged SLA, with its own business-time budget. A staged SLA tracks work that passes through
/// several hands — triage, then repair, then verification — and gives each phase its own deadline, so a late
/// stage is visible immediately instead of being hidden inside one long overall budget.
/// </summary>
/// <param name="Key">The stage key.</param>
/// <param name="Name">The human-readable stage name.</param>
/// <param name="Duration">The stage's business-time budget.</param>
/// <param name="Order">The stage's position in the sequence, starting at one.</param>
public sealed record SlaStage(string Key, string Name, TimeSpan Duration, int Order);

/// <summary>A stage's runtime state on an SLA instance.</summary>
public sealed class SlaStageState
{
    /// <summary>Initializes a new instance of the <see cref="SlaStageState"/> class.</summary>
    /// <param name="stage">The stage.</param>
    /// <param name="startedOnUtc">When the stage started.</param>
    /// <param name="dueOnUtc">When the stage is due.</param>
    public SlaStageState(SlaStage stage, DateTimeOffset startedOnUtc, DateTimeOffset dueOnUtc)
    {
        ArgumentNullException.ThrowIfNull(stage);
        Stage = stage;
        StartedOnUtc = startedOnUtc;
        DueOnUtc = dueOnUtc;
    }

    /// <summary>Gets the stage.</summary>
    public SlaStage Stage { get; }

    /// <summary>Gets when the stage started.</summary>
    public DateTimeOffset StartedOnUtc { get; }

    /// <summary>Gets when the stage is due.</summary>
    public DateTimeOffset DueOnUtc { get; private set; }

    /// <summary>Gets when the stage finished, if it has.</summary>
    public DateTimeOffset? CompletedOnUtc { get; private set; }

    /// <summary>Gets a value indicating whether the stage finished within its budget.</summary>
    public bool? Met => CompletedOnUtc is { } completed ? completed <= DueOnUtc : null;

    /// <summary>Marks the stage finished.</summary>
    /// <param name="completedOnUtc">When it finished.</param>
    public void Complete(DateTimeOffset completedOnUtc) => CompletedOnUtc = completedOnUtc;

    /// <summary>Moves the stage's due time (used when a pause shifts the schedule).</summary>
    /// <param name="dueOnUtc">The new due time.</param>
    public void RescheduleTo(DateTimeOffset dueOnUtc) => DueOnUtc = dueOnUtc;
}
