using FactoryOS.Plugins.Workflow.SLA.Configuration;
using FactoryOS.Plugins.Workflow.SLA.Diagnostics;
using FactoryOS.Plugins.Workflow.SLA.Domain;

namespace FactoryOS.Plugins.Workflow.SLA.Execution;

/// <summary>
/// The public entry point to the SLA engine. It registers definitions and business calendars, starts an SLA
/// over a workflow activity, human task, approval or form submission, stops and restarts its clock, advances
/// staged SLAs, closes them when the tracked work finishes, cancels them, runs the due-work pass, and reads
/// SLAs, history and metrics back.
/// <para>
/// An SLA is <b>attached deliberately</b> to a piece of work rather than inferred from an event: the caller
/// starts it and tells it when the work finished. The engine therefore never subscribes to, references or
/// modifies the workflow, human task, approval, forms or notification engines — it only publishes its own
/// events, which anything above it may consume.
/// </para>
/// </summary>
public sealed class SlaEngine
{
    private readonly SlaRuntime _runtime;
    private readonly CalendarEngine _calendars;
    private readonly SlaPermissionEvaluator _permissions;
    private readonly SlaMetrics _metrics;

    /// <summary>Initializes a new instance of the <see cref="SlaEngine"/> class.</summary>
    /// <param name="runtime">The SLA runtime.</param>
    /// <param name="calendars">The calendar engine.</param>
    /// <param name="permissions">The permission evaluator.</param>
    /// <param name="metrics">The metrics counters.</param>
    public SlaEngine(
        SlaRuntime runtime,
        CalendarEngine calendars,
        SlaPermissionEvaluator permissions,
        SlaMetrics metrics)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(calendars);
        ArgumentNullException.ThrowIfNull(permissions);
        ArgumentNullException.ThrowIfNull(metrics);
        _runtime = runtime;
        _calendars = calendars;
        _permissions = permissions;
        _metrics = metrics;
    }

    /// <summary>Registers an SLA definition.</summary>
    /// <param name="definition">The definition.</param>
    public void Register(SlaDefinition definition) => _runtime.Register(definition);

    /// <summary>Registers a business calendar (working hours and holidays) that policies may name.</summary>
    /// <param name="calendar">The calendar.</param>
    public void RegisterCalendar(BusinessCalendar calendar) => _calendars.Register(calendar);

    /// <summary>Starts an SLA tracking a target.</summary>
    /// <param name="definition">The SLA definition.</param>
    /// <param name="target">The work to track.</param>
    /// <param name="context">The context (tenant, initiator, culture).</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The started SLA.</returns>
    public Task<SlaInstance> StartAsync(
        SlaDefinition definition,
        SlaTarget target,
        SlaContext context,
        CancellationToken cancellationToken = default) =>
        _runtime.StartAsync(definition, target, context, cancellationToken);

    /// <summary>Stops an SLA's clock.</summary>
    /// <param name="slaId">The SLA id.</param>
    /// <param name="reason">Why the clock is stopping.</param>
    /// <param name="actor">Who is stopping it.</param>
    /// <returns>The paused SLA, or <see langword="null"/> when it cannot be paused.</returns>
    public SlaInstance? Pause(Guid slaId, PauseReason? reason = null, string? actor = null) =>
        _runtime.Pause(slaId, reason, actor);

    /// <summary>Restarts an SLA's clock, shifting its remaining schedule past the paused interval.</summary>
    /// <param name="slaId">The SLA id.</param>
    /// <param name="reason">Why the clock is restarting.</param>
    /// <param name="actor">Who is restarting it.</param>
    /// <returns>The resumed SLA, or <see langword="null"/> when it was not paused.</returns>
    public SlaInstance? Resume(Guid slaId, ResumeReason? reason = null, string? actor = null) =>
        _runtime.Resume(slaId, reason, actor);

    /// <summary>Advances a staged SLA to its next stage.</summary>
    /// <param name="slaId">The SLA id.</param>
    /// <param name="actor">Who is advancing it.</param>
    /// <returns>The updated SLA, or <see langword="null"/> when it cannot advance.</returns>
    public SlaInstance? AdvanceStage(Guid slaId, string? actor = null) => _runtime.AdvanceStage(slaId, actor);

    /// <summary>Closes an SLA because the tracked work finished.</summary>
    /// <param name="slaId">The SLA id.</param>
    /// <param name="actor">Who is closing it.</param>
    /// <returns>The completed SLA, or <see langword="null"/> when unknown or already finished.</returns>
    public SlaInstance? Complete(Guid slaId, string? actor = null) => _runtime.Complete(slaId, actor);

    /// <summary>Closes the open SLA tracking a target because that work finished.</summary>
    /// <param name="target">The tracked work.</param>
    /// <param name="actor">Who is closing it.</param>
    /// <returns>The completed SLA, or <see langword="null"/> when nothing open tracks the target.</returns>
    public SlaInstance? CompleteForTarget(SlaTarget target, string? actor = null) =>
        _runtime.CompleteForTarget(target, actor);

    /// <summary>Cancels an SLA before the tracked work finished.</summary>
    /// <param name="slaId">The SLA id.</param>
    /// <param name="actor">Who is cancelling it.</param>
    /// <param name="reason">An optional reason.</param>
    /// <returns>The cancelled SLA, or <see langword="null"/> when unknown or already finished.</returns>
    public SlaInstance? Cancel(Guid slaId, string? actor = null, string? reason = null) =>
        _runtime.Cancel(slaId, actor, reason);

    /// <summary>Runs the due-work pass (reminders, breaches, escalations, timeouts) over the open SLAs.</summary>
    /// <param name="cancellationToken">A token to cancel the pass.</param>
    /// <returns>A summary of the pass.</returns>
    public Task<SlaDueWorkSummary> RunDueAsync(CancellationToken cancellationToken = default) =>
        _runtime.RunDueAsync(cancellationToken);

    /// <summary>Gets an SLA by id.</summary>
    /// <param name="slaId">The SLA id.</param>
    /// <returns>The SLA, or <see langword="null"/> when not found.</returns>
    public SlaInstance? GetSla(Guid slaId) => _runtime.Get(slaId);

    /// <summary>Gets the open SLA tracking a target, if there is one.</summary>
    /// <param name="target">The tracked work.</param>
    /// <returns>The SLA, or <see langword="null"/>.</returns>
    public SlaInstance? ByTarget(SlaTarget target) => _runtime.ByTarget(target);

    /// <summary>Lists every SLA that has not finished.</summary>
    /// <returns>The open SLAs.</returns>
    public IReadOnlyCollection<SlaInstance> ListOpen() => _runtime.ListOpen();

    /// <summary>Gets the history entries of an SLA, oldest first.</summary>
    /// <param name="slaId">The SLA id.</param>
    /// <returns>The entries.</returns>
    public IReadOnlyList<SlaHistoryEntry> GetHistory(Guid slaId) => _runtime.GetHistory(slaId);

    /// <summary>Computes the rights a principal holds over an SLA.</summary>
    /// <param name="definition">The SLA definition supplying the grants.</param>
    /// <param name="sla">The SLA instance.</param>
    /// <param name="principal">The principal.</param>
    /// <param name="startedBy">Who started the SLA, if known.</param>
    /// <returns>The accumulated rights.</returns>
    public SlaPermission PermissionsFor(
        SlaDefinition definition, SlaInstance sla, string principal, string? startedBy = null) =>
        _permissions.Evaluate(definition, sla, principal, startedBy);

    /// <summary>Reads the engine's counters as a snapshot.</summary>
    /// <returns>The snapshot.</returns>
    public SlaMetricsSnapshot Snapshot() => _metrics.Snapshot();
}
