using FactoryOS.Domain.Abstractions;
using FactoryOS.Plugins.Workflow.Engine.Configuration;
using FactoryOS.Plugins.Workflow.Engine.Domain;
using FactoryOS.Plugins.Workflow.Engine.Persistence;

namespace FactoryOS.Plugins.Workflow.Engine.Execution;

/// <summary>A timer that is due to fire: the instance and node it belongs to and its due instant.</summary>
/// <param name="InstanceId">The instance id.</param>
/// <param name="NodeId">The timer node id.</param>
/// <param name="DueUtc">When the timer is due.</param>
public sealed record DueTimer(Guid InstanceId, string NodeId, DateTimeOffset DueUtc);

/// <summary>
/// Fires due workflow timers. It scans running instances for pending timers whose due instant has passed and
/// resumes each through the runtime. A caller (a background service or a test) drives it; the scheduler holds
/// no threads of its own.
/// </summary>
public sealed class WorkflowScheduler
{
    private readonly IWorkflowStore _store;
    private readonly WorkflowRuntime _runtime;
    private readonly IDateTimeProvider _clock;
    private readonly WorkflowEngineOptions _options;

    /// <summary>Initializes a new instance of the <see cref="WorkflowScheduler"/> class.</summary>
    /// <param name="store">The instance store.</param>
    /// <param name="runtime">The runtime.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="options">The engine options.</param>
    public WorkflowScheduler(
        IWorkflowStore store, WorkflowRuntime runtime, IDateTimeProvider clock, WorkflowEngineOptions options)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(options);

        _store = store;
        _runtime = runtime;
        _clock = clock;
        _options = options;
    }

    /// <summary>Lists the timers currently due across running instances.</summary>
    /// <returns>The due timers.</returns>
    public IReadOnlyCollection<DueTimer> DueTimers()
    {
        var now = _clock.UtcNow;
        return _store.ListByStatus(WorkflowStatus.Running)
            .SelectMany(instance => instance.PendingTimers
                .Where(timer => timer.Value <= now)
                .Select(timer => new DueTimer(instance.Id, timer.Key, timer.Value)))
            .ToArray();
    }

    /// <summary>Fires every due timer, up to the configured batch size, and returns how many fired.</summary>
    /// <param name="cancellationToken">A token to cancel the pass.</param>
    /// <returns>The number of timers fired.</returns>
    public async Task<int> FireDueAsync(CancellationToken cancellationToken = default)
    {
        var fired = 0;
        foreach (var due in DueTimers().Take(_options.SchedulerBatchSize))
        {
            cancellationToken.ThrowIfCancellationRequested();
            await _runtime.FireTimerAsync(due.InstanceId, due.NodeId, cancellationToken).ConfigureAwait(false);
            fired++;
        }

        return fired;
    }
}
