using System.Collections.Concurrent;
using FactoryOS.Plugins.Forms.Engine.Domain;

namespace FactoryOS.Plugins.Forms.Engine.Events;

/// <summary>The base of a forms lifecycle event raised by the runtime and published onto the event bus seam.</summary>
/// <param name="Tenant">The owning tenant.</param>
/// <param name="OccurredOnUtc">When the event occurred.</param>
/// <param name="FormKey">The form the event concerns.</param>
public abstract record FormEvent(string Tenant, DateTimeOffset OccurredOnUtc, string FormKey);

/// <summary>Raised when a form definition is registered for the first time.</summary>
/// <param name="Tenant">The tenant.</param>
/// <param name="OccurredOnUtc">When it occurred.</param>
/// <param name="FormKey">The form key.</param>
/// <param name="Version">The registered version.</param>
public sealed record FormCreated(string Tenant, DateTimeOffset OccurredOnUtc, string FormKey, FormVersion Version)
    : FormEvent(Tenant, OccurredOnUtc, FormKey);

/// <summary>Raised when a new version of an existing form definition is registered.</summary>
/// <param name="Tenant">The tenant.</param>
/// <param name="OccurredOnUtc">When it occurred.</param>
/// <param name="FormKey">The form key.</param>
/// <param name="Version">The new version.</param>
public sealed record FormUpdated(string Tenant, DateTimeOffset OccurredOnUtc, string FormKey, FormVersion Version)
    : FormEvent(Tenant, OccurredOnUtc, FormKey);

/// <summary>Raised when a form definition is published and becomes openable.</summary>
/// <param name="Tenant">The tenant.</param>
/// <param name="OccurredOnUtc">When it occurred.</param>
/// <param name="FormKey">The form key.</param>
/// <param name="Version">The published version.</param>
public sealed record FormPublished(string Tenant, DateTimeOffset OccurredOnUtc, string FormKey, FormVersion Version)
    : FormEvent(Tenant, OccurredOnUtc, FormKey);

/// <summary>The base of an event that concerns a single form instance.</summary>
/// <param name="Tenant">The tenant.</param>
/// <param name="OccurredOnUtc">When it occurred.</param>
/// <param name="FormKey">The form key.</param>
/// <param name="InstanceId">The instance the event concerns.</param>
public abstract record FormInstanceEvent(string Tenant, DateTimeOffset OccurredOnUtc, string FormKey, Guid InstanceId)
    : FormEvent(Tenant, OccurredOnUtc, FormKey);

/// <summary>Raised when a form instance is opened.</summary>
/// <param name="Tenant">The tenant.</param>
/// <param name="OccurredOnUtc">When it occurred.</param>
/// <param name="FormKey">The form key.</param>
/// <param name="InstanceId">The instance.</param>
/// <param name="Assignee">The resolved assignee, if any.</param>
public sealed record FormOpened(
    string Tenant, DateTimeOffset OccurredOnUtc, string FormKey, Guid InstanceId, string? Assignee)
    : FormInstanceEvent(Tenant, OccurredOnUtc, FormKey, InstanceId);

/// <summary>Raised when a draft is saved on a form instance.</summary>
/// <param name="Tenant">The tenant.</param>
/// <param name="OccurredOnUtc">When it occurred.</param>
/// <param name="FormKey">The form key.</param>
/// <param name="InstanceId">The instance.</param>
public sealed record FormSaved(string Tenant, DateTimeOffset OccurredOnUtc, string FormKey, Guid InstanceId)
    : FormInstanceEvent(Tenant, OccurredOnUtc, FormKey, InstanceId);

/// <summary>Raised when a form instance is submitted with values that passed validation.</summary>
/// <param name="Tenant">The tenant.</param>
/// <param name="OccurredOnUtc">When it occurred.</param>
/// <param name="FormKey">The form key.</param>
/// <param name="InstanceId">The instance.</param>
/// <param name="SubmissionId">The captured submission.</param>
/// <param name="SubmittedBy">Who submitted it.</param>
public sealed record FormSubmitted(
    string Tenant, DateTimeOffset OccurredOnUtc, string FormKey, Guid InstanceId, Guid SubmissionId, string? SubmittedBy)
    : FormInstanceEvent(Tenant, OccurredOnUtc, FormKey, InstanceId);

/// <summary>Raised when a submitted form instance is approved.</summary>
/// <param name="Tenant">The tenant.</param>
/// <param name="OccurredOnUtc">When it occurred.</param>
/// <param name="FormKey">The form key.</param>
/// <param name="InstanceId">The instance.</param>
public sealed record FormApproved(string Tenant, DateTimeOffset OccurredOnUtc, string FormKey, Guid InstanceId)
    : FormInstanceEvent(Tenant, OccurredOnUtc, FormKey, InstanceId);

/// <summary>Raised when a submitted form instance is rejected.</summary>
/// <param name="Tenant">The tenant.</param>
/// <param name="OccurredOnUtc">When it occurred.</param>
/// <param name="FormKey">The form key.</param>
/// <param name="InstanceId">The instance.</param>
/// <param name="Reason">The rejection reason, if given.</param>
public sealed record FormRejected(
    string Tenant, DateTimeOffset OccurredOnUtc, string FormKey, Guid InstanceId, string? Reason)
    : FormInstanceEvent(Tenant, OccurredOnUtc, FormKey, InstanceId);

/// <summary>Raised when a form instance is cancelled before completion.</summary>
/// <param name="Tenant">The tenant.</param>
/// <param name="OccurredOnUtc">When it occurred.</param>
/// <param name="FormKey">The form key.</param>
/// <param name="InstanceId">The instance.</param>
public sealed record FormCancelled(string Tenant, DateTimeOffset OccurredOnUtc, string FormKey, Guid InstanceId)
    : FormInstanceEvent(Tenant, OccurredOnUtc, FormKey, InstanceId);

/// <summary>Receives forms lifecycle events raised by the runtime. The seam onto the platform event bus.</summary>
public interface IFormEventSink
{
    /// <summary>Publishes a forms event.</summary>
    /// <param name="formEvent">The event to publish.</param>
    void Publish(FormEvent formEvent);
}

/// <summary>An in-memory <see cref="IFormEventSink"/> that records published events for inspection.</summary>
public sealed class InMemoryFormEventSink : IFormEventSink
{
    private readonly ConcurrentQueue<FormEvent> _events = new();

    /// <summary>Gets the published events in order.</summary>
    public IReadOnlyList<FormEvent> Events => _events.ToArray();

    /// <inheritdoc />
    public void Publish(FormEvent formEvent)
    {
        ArgumentNullException.ThrowIfNull(formEvent);
        _events.Enqueue(formEvent);
    }
}
