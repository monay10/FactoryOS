using FactoryOS.Plugins.Workflow.Engine.Domain;
using FactoryOS.Plugins.Workflow.Engine.Events;
using FactoryOS.Plugins.Workflow.Engine.Nodes;

namespace FactoryOS.Plugins.Workflow.Engine.Execution;

/// <summary>
/// The token-based execution core. It drives a workflow instance by advancing its active node tokens: it
/// runs every immediately-runnable node (start, decision, split/join, script, service, end) and settles
/// tokens on the nodes that wait for the outside world (activity, timer, signal). Waiting resumes through
/// the resume methods. The executor is stateless — all state lives on the instance — so it is safe to share.
/// </summary>
public sealed class WorkflowExecutor
{
    /// <summary>Starts an instance: places a token on the start node and advances.</summary>
    /// <param name="context">The execution context.</param>
    /// <param name="cancellationToken">A token to cancel execution.</param>
    /// <returns>The execution result.</returns>
    public Task<ExecutionResult> StartAsync(WorkflowExecutionContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.Instance.MarkRunning();
        context.Instance.AddToken(context.Definition.StartNodeId);
        return AdvanceAsync(context, cancellationToken);
    }

    /// <summary>Advances an instance until no node is immediately runnable.</summary>
    /// <param name="context">The execution context.</param>
    /// <param name="cancellationToken">A token to cancel execution.</param>
    /// <returns>The execution result.</returns>
    public async Task<ExecutionResult> AdvanceAsync(
        WorkflowExecutionContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var instance = context.Instance;

        if (instance.IsFinished)
        {
            return ExecutionResult.From(instance);
        }

        while (FindRunnable(context) is { } nodeId)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!await ExecuteNodeAsync(context, context.Definition.Node(nodeId), cancellationToken).ConfigureAwait(false))
            {
                return ExecutionResult.From(instance); // faulted
            }
        }

        if (instance.ActiveTokens.Count == 0)
        {
            instance.MarkCompleted();
            context.Events.Publish(new WorkflowCompleted(instance.Id, instance.Tenant, context.Clock.UtcNow));
        }
        else
        {
            instance.MarkRunning();
        }

        return ExecutionResult.From(instance);
    }

    /// <summary>Completes a pending activity, applies its outcome variables and advances.</summary>
    /// <param name="context">The execution context.</param>
    /// <param name="nodeId">The activity node id.</param>
    /// <param name="outcome">Optional variables to set from the activity outcome.</param>
    /// <param name="cancellationToken">A token to cancel execution.</param>
    /// <returns>The execution result.</returns>
    public async Task<ExecutionResult> ResumeActivityAsync(
        WorkflowExecutionContext context,
        string nodeId,
        IReadOnlyDictionary<string, object?>? outcome,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(nodeId);
        var instance = context.Instance;

        if (!instance.PendingActivities.ContainsKey(nodeId) || instance.TokensOn(nodeId) == 0)
        {
            return ExecutionResult.From(instance);
        }

        instance.RemoveToken(nodeId);
        instance.RemovePendingActivity(nodeId);

        if (outcome is not null)
        {
            foreach (var (name, value) in outcome)
            {
                instance.Variables.Set(name, value);
            }
        }

        context.Events.Publish(new ActivityCompleted(instance.Id, instance.Tenant, context.Clock.UtcNow, nodeId));
        instance.History.Append(context.Clock.UtcNow, nodeId, WorkflowState.Completed, "activity completed");

        return await ForwardAndAdvanceAsync(context, nodeId, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Fires a due timer and advances.</summary>
    /// <param name="context">The execution context.</param>
    /// <param name="nodeId">The timer node id.</param>
    /// <param name="cancellationToken">A token to cancel execution.</param>
    /// <returns>The execution result.</returns>
    public async Task<ExecutionResult> ResumeTimerAsync(
        WorkflowExecutionContext context, string nodeId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(nodeId);
        var instance = context.Instance;

        if (!instance.PendingTimers.ContainsKey(nodeId) || instance.TokensOn(nodeId) == 0)
        {
            return ExecutionResult.From(instance);
        }

        instance.RemoveToken(nodeId);
        instance.RemovePendingTimer(nodeId);
        instance.History.Append(context.Clock.UtcNow, nodeId, WorkflowState.Completed, "timer fired");

        return await ForwardAndAdvanceAsync(context, nodeId, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Delivers a named signal to a waiting node and advances.</summary>
    /// <param name="context">The execution context.</param>
    /// <param name="signalName">The signal name.</param>
    /// <param name="cancellationToken">A token to cancel execution.</param>
    /// <returns>The execution result.</returns>
    public async Task<ExecutionResult> ResumeSignalAsync(
        WorkflowExecutionContext context, string signalName, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(signalName);
        var instance = context.Instance;

        var waiting = context.Definition.Nodes
            .OfType<WaitNode>()
            .FirstOrDefault(node =>
                string.Equals(node.SignalName, signalName, StringComparison.Ordinal) && instance.TokensOn(node.Id) > 0);

        if (waiting is null)
        {
            return ExecutionResult.From(instance);
        }

        instance.RemoveToken(waiting.Id);
        instance.History.Append(context.Clock.UtcNow, waiting.Id, WorkflowState.Completed, $"signal '{signalName}'");

        return await ForwardAndAdvanceAsync(context, waiting.Id, cancellationToken).ConfigureAwait(false);
    }

    private async Task<ExecutionResult> ForwardAndAdvanceAsync(
        WorkflowExecutionContext context, string nodeId, CancellationToken cancellationToken)
    {
        if (!TryForward(context, nodeId, out var target))
        {
            return ExecutionResult.From(context.Instance);
        }

        Enter(context, target);
        return await AdvanceAsync(context, cancellationToken).ConfigureAwait(false);
    }

    private static string? FindRunnable(WorkflowExecutionContext context)
    {
        var instance = context.Instance;
        foreach (var nodeId in instance.ActiveTokens.Distinct(StringComparer.Ordinal))
        {
            var node = context.Definition.Node(nodeId);
            switch (node.Kind)
            {
                case WorkflowNodeKind.Start or WorkflowNodeKind.End or WorkflowNodeKind.Decision
                    or WorkflowNodeKind.Parallel or WorkflowNodeKind.Script or WorkflowNodeKind.Service:
                    return nodeId;
                case WorkflowNodeKind.Merge
                    when instance.TokensOn(nodeId) >= context.Definition.IncomingCount(nodeId):
                    return nodeId;
                default:
                    break; // Activity, Timer, Wait, or a merge still waiting for branches.
            }
        }

        return null;
    }

    private static async Task<bool> ExecuteNodeAsync(
        WorkflowExecutionContext context, WorkflowNode node, CancellationToken cancellationToken)
    {
        var instance = context.Instance;
        var now = context.Clock.UtcNow;

        switch (node)
        {
            case StartNode:
                instance.RemoveToken(node.Id);
                instance.History.Append(now, node.Id, WorkflowState.Completed, "start");
                return Forward(context, node.Id);

            case EndNode:
                instance.RemoveToken(node.Id);
                instance.History.Append(now, node.Id, WorkflowState.Completed, "end");
                return true;

            case DecisionNode:
                instance.RemoveToken(node.Id);
                var chosen = context.Definition.Outgoing(node.Id)
                    .FirstOrDefault(transition => transition.IsSatisfiedBy(instance.Variables));
                if (chosen is null)
                {
                    return Fail(context, node.Id, "no outgoing transition condition was satisfied");
                }

                instance.History.Append(now, node.Id, WorkflowState.Completed, $"decided → {chosen.To}");
                Enter(context, chosen.To);
                return true;

            case ParallelNode:
                instance.RemoveToken(node.Id);
                var branches = context.Definition.Outgoing(node.Id);
                if (branches.Count == 0)
                {
                    return Fail(context, node.Id, "a parallel split needs at least one outgoing transition");
                }

                instance.History.Append(now, node.Id, WorkflowState.Completed, $"forked ×{branches.Count}");
                foreach (var branch in branches)
                {
                    Enter(context, branch.To);
                }

                return true;

            case MergeNode:
                var incoming = context.Definition.IncomingCount(node.Id);
                for (var i = 0; i < incoming; i++)
                {
                    instance.RemoveToken(node.Id);
                }

                instance.History.Append(now, node.Id, WorkflowState.Completed, "joined");
                return Forward(context, node.Id);

            case ScriptNode script:
                instance.RemoveToken(node.Id);
                foreach (var assignment in script.Assignments)
                {
                    instance.Variables.Set(assignment.Variable, assignment.Evaluate(instance.Variables));
                }

                instance.History.Append(now, node.Id, WorkflowState.Completed, "script applied");
                return Forward(context, node.Id);

            case ServiceNode service:
                return await ExecuteServiceAsync(context, service, cancellationToken).ConfigureAwait(false);

            default:
                // Activity, Timer and Wait nodes never reach here (they are not runnable).
                return true;
        }
    }

    private static async Task<bool> ExecuteServiceAsync(
        WorkflowExecutionContext context, ServiceNode node, CancellationToken cancellationToken)
    {
        var instance = context.Instance;
        instance.RemoveToken(node.Id);
        context.Events.Publish(new ActivityStarted(instance.Id, instance.Tenant, context.Clock.UtcNow, node.Id, null));

        var service = context.Services.Find(node.ServiceKey);
        if (service is null)
        {
            context.Events.Publish(new ActivityFailed(
                instance.Id, instance.Tenant, context.Clock.UtcNow, node.Id, $"no service '{node.ServiceKey}'"));
            return Fail(context, node.Id, $"no service registered for key '{node.ServiceKey}'");
        }

        try
        {
            await service.ExecuteAsync(context.ScopeFor(node), cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            context.Events.Publish(new ActivityFailed(
                instance.Id, instance.Tenant, context.Clock.UtcNow, node.Id, exception.Message));
            return Fail(context, node.Id, exception.Message);
        }

        context.Events.Publish(new ActivityCompleted(instance.Id, instance.Tenant, context.Clock.UtcNow, node.Id));
        instance.History.Append(context.Clock.UtcNow, node.Id, WorkflowState.Completed, "service invoked");
        return Forward(context, node.Id);
    }

    private static void Enter(WorkflowExecutionContext context, string nodeId)
    {
        var instance = context.Instance;
        var node = context.Definition.Node(nodeId);
        var now = context.Clock.UtcNow;
        instance.AddToken(nodeId);

        switch (node)
        {
            case ActivityNode activity:
                var assignee = activity.Assignment?.Resolve(instance.Variables);
                instance.AddPendingActivity(new PendingActivity(activity.Id, activity.ActivityKey, assignee));
                instance.History.Append(now, activity.Id, WorkflowState.Waiting, "activity pending");
                context.Events.Publish(
                    new ActivityStarted(instance.Id, instance.Tenant, now, activity.Id, assignee));
                break;

            case TimerNode timer:
                instance.AddPendingTimer(timer.Id, now + timer.Delay);
                instance.History.Append(now, timer.Id, WorkflowState.Waiting, "timer scheduled");
                break;

            case WaitNode wait:
                instance.History.Append(now, wait.Id, WorkflowState.Waiting, $"awaiting signal '{wait.SignalName}'");
                break;

            default:
                break; // runnable nodes have no entry side effect
        }
    }

    private static bool Forward(WorkflowExecutionContext context, string nodeId)
    {
        if (!TryForward(context, nodeId, out var target))
        {
            return false;
        }

        Enter(context, target);
        return true;
    }

    private static bool TryForward(WorkflowExecutionContext context, string nodeId, out string target)
    {
        var outgoing = context.Definition.Outgoing(nodeId);
        if (outgoing.Count == 0)
        {
            Fail(context, nodeId, "no outgoing transition");
            target = string.Empty;
            return false;
        }

        target = outgoing[0].To;
        return true;
    }

    private static bool Fail(WorkflowExecutionContext context, string nodeId, string reason)
    {
        var instance = context.Instance;
        instance.History.Append(context.Clock.UtcNow, nodeId, WorkflowState.Faulted, reason);
        instance.MarkFailed($"{nodeId}: {reason}");
        context.Events.Publish(
            new WorkflowFailed(instance.Id, instance.Tenant, context.Clock.UtcNow, instance.FailureReason!));
        return false;
    }
}
