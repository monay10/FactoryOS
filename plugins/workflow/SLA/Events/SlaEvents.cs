using System.Collections.Concurrent;
using FactoryOS.Plugins.Workflow.SLA.Domain;

namespace FactoryOS.Plugins.Workflow.SLA.Events;

/// <summary>The base of an SLA lifecycle event raised by the runtime and published onto the event bus.</summary>
/// <param name="SlaId">The SLA the event concerns.</param>
/// <param name="Tenant">The owning tenant.</param>
/// <param name="OccurredOnUtc">When the event occurred.</param>
/// <param name="DefinitionKey">The SLA definition.</param>
/// <param name="Target">The work being tracked.</param>
public abstract record SlaEvent(
    Guid SlaId, string Tenant, DateTimeOffset OccurredOnUtc, string DefinitionKey, SlaTarget Target);

/// <summary>Raised when an SLA starts tracking a target.</summary>
/// <param name="SlaId">The SLA.</param>
/// <param name="Tenant">The tenant.</param>
/// <param name="OccurredOnUtc">When it occurred.</param>
/// <param name="DefinitionKey">The definition.</param>
/// <param name="Target">The tracked work.</param>
/// <param name="DueOnUtc">When the deadline falls.</param>
public sealed record SlaStarted(
    Guid SlaId, string Tenant, DateTimeOffset OccurredOnUtc, string DefinitionKey, SlaTarget Target,
    DateTimeOffset DueOnUtc)
    : SlaEvent(SlaId, Tenant, OccurredOnUtc, DefinitionKey, Target);

/// <summary>Raised when an SLA's clock is stopped.</summary>
/// <param name="SlaId">The SLA.</param>
/// <param name="Tenant">The tenant.</param>
/// <param name="OccurredOnUtc">When it occurred.</param>
/// <param name="DefinitionKey">The definition.</param>
/// <param name="Target">The tracked work.</param>
/// <param name="Reason">Why the clock stopped.</param>
public sealed record SlaPaused(
    Guid SlaId, string Tenant, DateTimeOffset OccurredOnUtc, string DefinitionKey, SlaTarget Target,
    PauseReason Reason)
    : SlaEvent(SlaId, Tenant, OccurredOnUtc, DefinitionKey, Target);

/// <summary>Raised when an SLA's clock is restarted and its remaining schedule shifts forward.</summary>
/// <param name="SlaId">The SLA.</param>
/// <param name="Tenant">The tenant.</param>
/// <param name="OccurredOnUtc">When it occurred.</param>
/// <param name="DefinitionKey">The definition.</param>
/// <param name="Target">The tracked work.</param>
/// <param name="Reason">Why the clock restarted.</param>
/// <param name="DueOnUtc">The deadline after the shift.</param>
public sealed record SlaResumed(
    Guid SlaId, string Tenant, DateTimeOffset OccurredOnUtc, string DefinitionKey, SlaTarget Target,
    ResumeReason Reason, DateTimeOffset DueOnUtc)
    : SlaEvent(SlaId, Tenant, OccurredOnUtc, DefinitionKey, Target);

/// <summary>Raised when a reminder fires ahead of the deadline.</summary>
/// <param name="SlaId">The SLA.</param>
/// <param name="Tenant">The tenant.</param>
/// <param name="OccurredOnUtc">When it occurred.</param>
/// <param name="DefinitionKey">The definition.</param>
/// <param name="Target">The tracked work.</param>
/// <param name="Before">How far ahead of the deadline the reminder was set.</param>
public sealed record SlaReminderTriggered(
    Guid SlaId, string Tenant, DateTimeOffset OccurredOnUtc, string DefinitionKey, SlaTarget Target, TimeSpan Before)
    : SlaEvent(SlaId, Tenant, OccurredOnUtc, DefinitionKey, Target);

/// <summary>Raised when an escalation rung fires after the deadline.</summary>
/// <param name="SlaId">The SLA.</param>
/// <param name="Tenant">The tenant.</param>
/// <param name="OccurredOnUtc">When it occurred.</param>
/// <param name="DefinitionKey">The definition.</param>
/// <param name="Target">The tracked work.</param>
/// <param name="Level">The escalation level reached.</param>
/// <param name="Assignee">Who the work escalated to.</param>
public sealed record SlaEscalated(
    Guid SlaId, string Tenant, DateTimeOffset OccurredOnUtc, string DefinitionKey, SlaTarget Target,
    int Level, string Assignee)
    : SlaEvent(SlaId, Tenant, OccurredOnUtc, DefinitionKey, Target);

/// <summary>
/// Raised when the deadline passes while the work is still open — the SLA is breached but keeps running, so
/// escalations continue. Distinct from <see cref="SlaTimedOut"/>, which ends the SLA.
/// </summary>
/// <param name="SlaId">The SLA.</param>
/// <param name="Tenant">The tenant.</param>
/// <param name="OccurredOnUtc">When it occurred.</param>
/// <param name="DefinitionKey">The definition.</param>
/// <param name="Target">The tracked work.</param>
/// <param name="DueOnUtc">The deadline that was missed.</param>
public sealed record SlaExpired(
    Guid SlaId, string Tenant, DateTimeOffset OccurredOnUtc, string DefinitionKey, SlaTarget Target,
    DateTimeOffset DueOnUtc)
    : SlaEvent(SlaId, Tenant, OccurredOnUtc, DefinitionKey, Target);

/// <summary>
/// Raised when the hard timeout passes: the SLA stops waiting for the work and finishes as
/// <see cref="SlaOutcome.TimedOut"/>. Distinct from <see cref="SlaExpired"/>, which only records a missed
/// deadline.
/// </summary>
/// <param name="SlaId">The SLA.</param>
/// <param name="Tenant">The tenant.</param>
/// <param name="OccurredOnUtc">When it occurred.</param>
/// <param name="DefinitionKey">The definition.</param>
/// <param name="Target">The tracked work.</param>
public sealed record SlaTimedOut(
    Guid SlaId, string Tenant, DateTimeOffset OccurredOnUtc, string DefinitionKey, SlaTarget Target)
    : SlaEvent(SlaId, Tenant, OccurredOnUtc, DefinitionKey, Target);

/// <summary>Raised when the tracked work finishes and the SLA closes.</summary>
/// <param name="SlaId">The SLA.</param>
/// <param name="Tenant">The tenant.</param>
/// <param name="OccurredOnUtc">When it occurred.</param>
/// <param name="DefinitionKey">The definition.</param>
/// <param name="Target">The tracked work.</param>
/// <param name="Outcome">Whether the deadline was met or breached.</param>
public sealed record SlaCompleted(
    Guid SlaId, string Tenant, DateTimeOffset OccurredOnUtc, string DefinitionKey, SlaTarget Target,
    SlaOutcome Outcome)
    : SlaEvent(SlaId, Tenant, OccurredOnUtc, DefinitionKey, Target);

/// <summary>Raised when an SLA is cancelled before the tracked work finished.</summary>
/// <param name="SlaId">The SLA.</param>
/// <param name="Tenant">The tenant.</param>
/// <param name="OccurredOnUtc">When it occurred.</param>
/// <param name="DefinitionKey">The definition.</param>
/// <param name="Target">The tracked work.</param>
public sealed record SlaCancelled(
    Guid SlaId, string Tenant, DateTimeOffset OccurredOnUtc, string DefinitionKey, SlaTarget Target)
    : SlaEvent(SlaId, Tenant, OccurredOnUtc, DefinitionKey, Target);

/// <summary>
/// Receives SLA lifecycle events raised by the runtime — the seam onto the platform event bus. Unlike the other
/// engines' single-sink seams, the runtime fans out to <b>every</b> registered sink, so several consumers (for
/// example the in-memory recorder and the notification bridge) can observe the same SLA stream at once.
/// </summary>
public interface ISlaEventSink
{
    /// <summary>Publishes an SLA event.</summary>
    /// <param name="slaEvent">The event to publish.</param>
    void Publish(SlaEvent slaEvent);
}

/// <summary>An in-memory <see cref="ISlaEventSink"/> that records published events for inspection.</summary>
public sealed class InMemorySlaEventSink : ISlaEventSink
{
    private readonly ConcurrentQueue<SlaEvent> _events = new();

    /// <summary>Gets the published events in order.</summary>
    public IReadOnlyList<SlaEvent> Events => _events.ToArray();

    /// <inheritdoc />
    public void Publish(SlaEvent slaEvent)
    {
        ArgumentNullException.ThrowIfNull(slaEvent);
        _events.Enqueue(slaEvent);
    }
}
