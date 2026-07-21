using FactoryOS.Domain.Abstractions;
using FactoryOS.IntegrationTests.Persistence;
using FactoryOS.Plugins.Workflow.Engine.Configuration;
using FactoryOS.Plugins.Workflow.Engine.Domain;
using FactoryOS.Plugins.Workflow.Engine.Events;
using FactoryOS.Plugins.Workflow.Engine.Execution;
using FactoryOS.Plugins.Workflow.Engine.Nodes;
using Microsoft.Extensions.DependencyInjection;

namespace FactoryOS.IntegrationTests.Workflow;

/// <summary>
/// The workflow engine composed through <c>AddWorkflowEngine</c> against a real container: definitions run
/// end-to-end through the resolved engine and scheduler — a sequential activity flow, a parallel split/join,
/// a decision branch, a timer resumed by the scheduler and a cancellation — and the wired event sink records
/// the lifecycle events.
/// </summary>
public sealed class WorkflowEngineIntegrationTests
{
    private static readonly DateTimeOffset Now = new(2026, 07, 21, 09, 00, 00, TimeSpan.Zero);

    private static (ServiceProvider Provider, FixedClock Clock) Build()
    {
        var clock = new FixedClock(Now);
        var services = new ServiceCollection();
        services.AddSingleton<IDateTimeProvider>(clock); // wins over the engine's TryAdd default
        services.AddWorkflowEngine();
        return (services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true }), clock);
    }

    private static WorkflowDefinition Sequential() => WorkflowDefinition.Create("seq", "Sequential")
        .AddNode(new StartNode("s"))
        .AddNode(new ActivityNode("task", "Task", "task"))
        .AddNode(new EndNode("e"))
        .AddTransition("s", "task")
        .AddTransition("task", "e")
        .Build();

    [Fact]
    public async Task A_sequential_activity_flow_runs_end_to_end()
    {
        var (provider, _) = Build();
        using var scope = provider;
        var engine = provider.GetRequiredService<WorkflowEngine>();
        var events = (InMemoryWorkflowEventSink)provider.GetRequiredService<IWorkflowEventSink>();

        var started = await engine.StartAsync(Sequential(), WorkflowContext.Default);
        Assert.True(started.IsRunning);

        var completed = await engine.CompleteActivityAsync(started.InstanceId, "task");
        Assert.True(completed!.IsCompleted);
        Assert.Contains(events.Events, e => e is WorkflowStarted);
        Assert.Contains(events.Events, e => e is WorkflowCompleted);
    }

    [Fact]
    public async Task A_parallel_flow_and_a_decision_flow_complete()
    {
        var (provider, _) = Build();
        using var scope = provider;
        var engine = provider.GetRequiredService<WorkflowEngine>();

        var parallel = WorkflowDefinition.Create("par", "Parallel")
            .AddNode(new StartNode("s")).AddNode(new ParallelNode("fork"))
            .AddNode(new ScriptNode("b1", [new ScriptAssignment("x", "1")]))
            .AddNode(new ScriptNode("b2", [new ScriptAssignment("y", "2")]))
            .AddNode(new MergeNode("join")).AddNode(new EndNode("e"))
            .AddTransition("s", "fork").AddTransition("fork", "b1").AddTransition("fork", "b2")
            .AddTransition("b1", "join").AddTransition("b2", "join").AddTransition("join", "e")
            .Build();
        var parallelResult = await engine.StartAsync(parallel, WorkflowContext.Default);
        Assert.True(parallelResult.IsCompleted);
        Assert.Equal(2m, engine.GetInstance(parallelResult.InstanceId)!.Variables.Get("y"));

        var decision = WorkflowDefinition.Create("dec", "Decision")
            .AddNode(new StartNode("s")).AddNode(new DecisionNode("d"))
            .AddNode(new EndNode("yes")).AddNode(new EndNode("no"))
            .AddTransition("s", "d").AddTransition("d", "yes", "approved == true").AddTransition("d", "no")
            .Build();
        var decisionResult = await engine.StartAsync(
            decision, WorkflowContext.Default, new Dictionary<string, object?> { ["approved"] = true });
        Assert.True(decisionResult.IsCompleted);
        Assert.Contains(engine.GetInstance(decisionResult.InstanceId)!.History.Entries, entry => entry.NodeId == "yes");
    }

    [Fact]
    public async Task A_timer_flow_is_resumed_by_the_scheduler()
    {
        var (provider, clock) = Build();
        using var scope = provider;
        var engine = provider.GetRequiredService<WorkflowEngine>();
        var scheduler = provider.GetRequiredService<WorkflowScheduler>();

        var definition = WorkflowDefinition.Create("timer", "Timer")
            .AddNode(new StartNode("s")).AddNode(new TimerNode("wait", TimeSpan.FromMinutes(30)))
            .AddNode(new EndNode("e"))
            .AddTransition("s", "wait").AddTransition("wait", "e")
            .Build();

        var result = await engine.StartAsync(definition, WorkflowContext.Default);
        Assert.Equal(0, await scheduler.FireDueAsync());

        clock.UtcNow = Now.AddHours(1);
        Assert.Equal(1, await scheduler.FireDueAsync());
        Assert.Equal(WorkflowStatus.Completed, engine.GetInstance(result.InstanceId)!.Status);
    }

    [Fact]
    public async Task A_running_instance_is_cancelled()
    {
        var (provider, _) = Build();
        using var scope = provider;
        var engine = provider.GetRequiredService<WorkflowEngine>();

        var result = await engine.StartAsync(Sequential(), WorkflowContext.Default);
        var cancelled = await engine.CancelAsync(result.InstanceId);

        Assert.Equal(WorkflowStatus.Cancelled, cancelled!.Status);
    }
}
