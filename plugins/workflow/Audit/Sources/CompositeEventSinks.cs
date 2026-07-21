using FactoryOS.Plugins.Forms.Engine.Events;
using FactoryOS.Plugins.Workflow.Approvals.Events;
using FactoryOS.Plugins.Workflow.Engine.Events;
using FactoryOS.Plugins.Workflow.Notifications.Events;
using FactoryOS.Plugins.Workflow.Tasks.Events;

namespace FactoryOS.Plugins.Workflow.Audit.Sources;

/// <summary>
/// Publishes a workflow event to several sinks in turn.
/// <para>
/// The workflow, forms, human task, approval and notification engines each expose a <b>single-registration</b>
/// event seam, so only one consumer can own it. That was fine while notifications were the only consumer, but
/// audit has to see the same stream. Rather than change those engines — which this commit may not do, and
/// should not have to — the audit layer wraps whatever is already registered in a composite and appends itself.
/// Every existing consumer keeps receiving its events, unchanged and unaware.
/// </para>
/// </summary>
public sealed class CompositeWorkflowEventSink : IWorkflowEventSink
{
    private readonly IReadOnlyList<IWorkflowEventSink> _sinks;

    /// <summary>Initializes a new instance of the <see cref="CompositeWorkflowEventSink"/> class.</summary>
    /// <param name="sinks">The sinks to publish to, in order.</param>
    public CompositeWorkflowEventSink(IEnumerable<IWorkflowEventSink> sinks)
    {
        ArgumentNullException.ThrowIfNull(sinks);
        _sinks = [.. sinks];
    }

    /// <inheritdoc />
    public void Publish(WorkflowEvent workflowEvent)
    {
        foreach (var sink in _sinks)
        {
            sink.Publish(workflowEvent);
        }
    }
}

/// <summary>Publishes a forms event to several sinks in turn. See <see cref="CompositeWorkflowEventSink"/>.</summary>
public sealed class CompositeFormEventSink : IFormEventSink
{
    private readonly IReadOnlyList<IFormEventSink> _sinks;

    /// <summary>Initializes a new instance of the <see cref="CompositeFormEventSink"/> class.</summary>
    /// <param name="sinks">The sinks to publish to, in order.</param>
    public CompositeFormEventSink(IEnumerable<IFormEventSink> sinks)
    {
        ArgumentNullException.ThrowIfNull(sinks);
        _sinks = [.. sinks];
    }

    /// <inheritdoc />
    public void Publish(FormEvent formEvent)
    {
        foreach (var sink in _sinks)
        {
            sink.Publish(formEvent);
        }
    }
}

/// <summary>Publishes a human task event to several sinks in turn. See <see cref="CompositeWorkflowEventSink"/>.</summary>
public sealed class CompositeHumanTaskEventSink : IHumanTaskEventSink
{
    private readonly IReadOnlyList<IHumanTaskEventSink> _sinks;

    /// <summary>Initializes a new instance of the <see cref="CompositeHumanTaskEventSink"/> class.</summary>
    /// <param name="sinks">The sinks to publish to, in order.</param>
    public CompositeHumanTaskEventSink(IEnumerable<IHumanTaskEventSink> sinks)
    {
        ArgumentNullException.ThrowIfNull(sinks);
        _sinks = [.. sinks];
    }

    /// <inheritdoc />
    public void Publish(HumanTaskEvent taskEvent)
    {
        foreach (var sink in _sinks)
        {
            sink.Publish(taskEvent);
        }
    }
}

/// <summary>Publishes an approval event to several sinks in turn. See <see cref="CompositeWorkflowEventSink"/>.</summary>
public sealed class CompositeApprovalEventSink : IApprovalEventSink
{
    private readonly IReadOnlyList<IApprovalEventSink> _sinks;

    /// <summary>Initializes a new instance of the <see cref="CompositeApprovalEventSink"/> class.</summary>
    /// <param name="sinks">The sinks to publish to, in order.</param>
    public CompositeApprovalEventSink(IEnumerable<IApprovalEventSink> sinks)
    {
        ArgumentNullException.ThrowIfNull(sinks);
        _sinks = [.. sinks];
    }

    /// <inheritdoc />
    public void Publish(ApprovalEvent approvalEvent)
    {
        foreach (var sink in _sinks)
        {
            sink.Publish(approvalEvent);
        }
    }
}

/// <summary>Publishes a notification event to several sinks in turn. See <see cref="CompositeWorkflowEventSink"/>.</summary>
public sealed class CompositeNotificationEventSink : INotificationEventSink
{
    private readonly IReadOnlyList<INotificationEventSink> _sinks;

    /// <summary>Initializes a new instance of the <see cref="CompositeNotificationEventSink"/> class.</summary>
    /// <param name="sinks">The sinks to publish to, in order.</param>
    public CompositeNotificationEventSink(IEnumerable<INotificationEventSink> sinks)
    {
        ArgumentNullException.ThrowIfNull(sinks);
        _sinks = [.. sinks];
    }

    /// <inheritdoc />
    public void Publish(NotificationEvent notificationEvent)
    {
        foreach (var sink in _sinks)
        {
            sink.Publish(notificationEvent);
        }
    }
}
