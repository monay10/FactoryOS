using FactoryOS.Domain.Abstractions;
using FactoryOS.Domain.Time;
using FactoryOS.Plugins.Workflow.Engine.Assignments;
using FactoryOS.Plugins.Workflow.Engine.Configuration;
using FactoryOS.Plugins.Workflow.Engine.Domain;
using FactoryOS.Plugins.Workflow.Engine.Events;
using FactoryOS.Plugins.Workflow.Engine.Execution;
using FactoryOS.Plugins.Workflow.Engine.Expressions;
using FactoryOS.Plugins.Workflow.Engine.Nodes;
using FactoryOS.Plugins.Workflow.Engine.Persistence;
using FactoryOS.Tests.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace FactoryOS.Tests.Workflow.EngineCore;

public sealed class WorkflowEngineCoreTests
{
    private static readonly DateTimeOffset Now = new(2026, 07, 21, 09, 00, 00, TimeSpan.Zero);

    private sealed class Harness
    {
        public MutableClock Clock { get; } = new(Now);

        public InMemoryWorkflowStore Store { get; } = new();

        public InMemoryWorkflowRepository Repository { get; } = new();

        public InMemoryWorkflowEventSink Events { get; } = new();

        public WorkflowEngine Engine { get; }

        public WorkflowRuntime Runtime { get; }

        public WorkflowScheduler Scheduler { get; }

        public Harness(params IWorkflowService[] services)
        {
            var registry = new WorkflowServiceRegistry(services);
            Runtime = new WorkflowRuntime(Store, Repository, new WorkflowExecutor(), Clock, registry, Events);
            var options = new WorkflowEngineOptions();
            Engine = new WorkflowEngine(Repository, Store, Runtime, options);
            Scheduler = new WorkflowScheduler(Store, Runtime, Clock, options);
        }

        public bool Has<T>() where T : WorkflowEvent => Events.Events.OfType<T>().Any();
    }

    // ---- Expressions ----------------------------------------------------------

    [Theory]
    [InlineData("amount > 100", true)]
    [InlineData("amount >= 150 && status == 'open'", true)]
    [InlineData("amount < 100 || status == 'closed'", false)]
    [InlineData("!(status == 'open')", false)]
    public void Expression_evaluates_conditions(string expression, bool expected)
    {
        var variables = new Dictionary<string, object?> { ["amount"] = 150, ["status"] = "open" };
        Assert.Equal(expected, WorkflowExpression.Parse(expression).EvaluateBoolean(variables));
    }

    [Fact]
    public void Expression_evaluates_arithmetic_and_precedence()
    {
        var variables = new Dictionary<string, object?> { ["a"] = 2, ["b"] = 3 };
        Assert.Equal(8m, WorkflowExpression.Parse("a + b * 2").Evaluate(variables));
        Assert.Equal(10m, WorkflowExpression.Parse("(a + b) * 2").Evaluate(variables));
        Assert.Equal("open-42", WorkflowExpression.Parse("'open-' + 42").Evaluate(variables));
    }

    // ---- Version & definition -------------------------------------------------

    [Fact]
    public void Version_orders_and_increments()
    {
        Assert.True(WorkflowVersion.Initial < WorkflowVersion.Initial.Next());
        Assert.Equal(new WorkflowVersion(2), WorkflowVersion.Initial.Next());
    }

    [Fact]
    public void Definition_requires_one_start_and_an_end()
    {
        Assert.Throws<InvalidOperationException>(() =>
            WorkflowDefinition.Create("d", "D").AddNode(new EndNode("e")).Build()); // no start

        Assert.Throws<InvalidOperationException>(() =>
            WorkflowDefinition.Create("d", "D").AddNode(new StartNode("s")).Build()); // no end

        Assert.Throws<InvalidOperationException>(() => WorkflowDefinition.Create("d", "D")
            .AddNode(new StartNode("s")).AddNode(new EndNode("e"))
            .AddTransition("s", "missing").Build()); // dangling transition
    }

    // ---- Assignments ----------------------------------------------------------

    [Fact]
    public void Assignments_resolve_their_assignee()
    {
        var variables = new WorkflowVariables(new Dictionary<string, object?> { ["approver"] = "alice" });

        Assert.Equal("bob", new UserAssignment("bob").Resolve(variables));
        Assert.Equal("role:Supervisor", new RoleAssignment("Supervisor").Resolve(variables));
        Assert.Equal("group:Plant", new GroupAssignment("Plant").Resolve(variables));
        Assert.Equal("user:alice", new DynamicAssignment("'user:' + approver").Resolve(variables));
    }

    // ---- Sequential + activity lifecycle --------------------------------------

    private static WorkflowDefinition ApprovalFlow() => WorkflowDefinition.Create("approval", "Approval")
        .AddNode(new StartNode("s"))
        .AddNode(new ActivityNode("review", "Review", "review", new RoleAssignment("Supervisor")))
        .AddNode(new EndNode("e"))
        .AddTransition("s", "review")
        .AddTransition("review", "e")
        .Build();

    [Fact]
    public async Task An_activity_waits_and_completes()
    {
        var harness = new Harness();
        var result = await harness.Engine.StartAsync(ApprovalFlow(), WorkflowContext.Default);

        Assert.True(result.IsRunning);
        var instance = harness.Engine.GetInstance(result.InstanceId)!;
        Assert.True(instance.PendingActivities.ContainsKey("review"));
        Assert.Equal("role:Supervisor", instance.PendingActivities["review"].Assignee);
        Assert.True(harness.Has<WorkflowStarted>());
        Assert.True(harness.Has<ActivityStarted>());

        var completed = await harness.Engine.CompleteActivityAsync(
            result.InstanceId, "review", new Dictionary<string, object?> { ["decision"] = "approved" });

        Assert.True(completed!.IsCompleted);
        Assert.Equal("approved", instance.Variables.Get("decision"));
        Assert.True(harness.Has<ActivityCompleted>());
        Assert.True(harness.Has<WorkflowCompleted>());
    }

    // ---- Decision -------------------------------------------------------------

    [Fact]
    public async Task A_decision_follows_the_first_satisfied_transition()
    {
        var definition = WorkflowDefinition.Create("triage", "Triage")
            .AddNode(new StartNode("s"))
            .AddNode(new DecisionNode("d"))
            .AddNode(new EndNode("high"))
            .AddNode(new EndNode("low"))
            .AddTransition("s", "d")
            .AddTransition("d", "high", "amount >= 100")
            .AddTransition("d", "low")
            .Build();
        var harness = new Harness();

        var result = await harness.Engine.StartAsync(
            definition, WorkflowContext.Default, new Dictionary<string, object?> { ["amount"] = 250 });

        Assert.True(result.IsCompleted);
        var instance = harness.Engine.GetInstance(result.InstanceId)!;
        Assert.Contains(instance.History.Entries, entry => entry.NodeId == "high");
        Assert.DoesNotContain(instance.History.Entries, entry => entry.NodeId == "low");
    }

    // ---- Parallel + merge -----------------------------------------------------

    [Fact]
    public async Task A_parallel_split_forks_and_a_merge_joins()
    {
        var definition = WorkflowDefinition.Create("parallel", "Parallel")
            .AddNode(new StartNode("s"))
            .AddNode(new ParallelNode("fork"))
            .AddNode(new ScriptNode("b1", [new ScriptAssignment("x", "1")]))
            .AddNode(new ScriptNode("b2", [new ScriptAssignment("y", "2")]))
            .AddNode(new MergeNode("join"))
            .AddNode(new EndNode("e"))
            .AddTransition("s", "fork")
            .AddTransition("fork", "b1")
            .AddTransition("fork", "b2")
            .AddTransition("b1", "join")
            .AddTransition("b2", "join")
            .AddTransition("join", "e")
            .Build();
        var harness = new Harness();

        var result = await harness.Engine.StartAsync(definition, WorkflowContext.Default);

        Assert.True(result.IsCompleted);
        var instance = harness.Engine.GetInstance(result.InstanceId)!;
        Assert.Equal(1m, instance.Variables.Get("x"));
        Assert.Equal(2m, instance.Variables.Get("y"));
        Assert.Contains(instance.History.Entries, entry => entry.NodeId == "join" && entry.Detail == "joined");
    }

    // ---- Service node ---------------------------------------------------------

    private sealed class StampService : IWorkflowService
    {
        public string Key => "stamp";

        public Task ExecuteAsync(ExecutionScope scope, CancellationToken cancellationToken)
        {
            scope.Variables.Set("stamped", true);
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task A_service_node_invokes_a_registered_service()
    {
        var definition = WorkflowDefinition.Create("svc", "Service")
            .AddNode(new StartNode("s"))
            .AddNode(new ServiceNode("call", "stamp"))
            .AddNode(new EndNode("e"))
            .AddTransition("s", "call")
            .AddTransition("call", "e")
            .Build();
        var harness = new Harness(new StampService());

        var result = await harness.Engine.StartAsync(definition, WorkflowContext.Default);

        Assert.True(result.IsCompleted);
        Assert.Equal(true, harness.Engine.GetInstance(result.InstanceId)!.Variables.Get("stamped"));
    }

    [Fact]
    public async Task A_missing_service_faults_the_instance()
    {
        var definition = WorkflowDefinition.Create("svc", "Service")
            .AddNode(new StartNode("s"))
            .AddNode(new ServiceNode("call", "absent"))
            .AddNode(new EndNode("e"))
            .AddTransition("s", "call")
            .AddTransition("call", "e")
            .Build();
        var harness = new Harness();

        var result = await harness.Engine.StartAsync(definition, WorkflowContext.Default);

        Assert.True(result.IsFailed);
        Assert.True(harness.Has<WorkflowFailed>());
        Assert.True(harness.Has<ActivityFailed>());
    }

    // ---- Timer + scheduler ----------------------------------------------------

    [Fact]
    public async Task A_timer_waits_until_due_then_the_scheduler_fires_it()
    {
        var definition = WorkflowDefinition.Create("timer", "Timer")
            .AddNode(new StartNode("s"))
            .AddNode(new TimerNode("wait", TimeSpan.FromMinutes(10)))
            .AddNode(new EndNode("e"))
            .AddTransition("s", "wait")
            .AddTransition("wait", "e")
            .Build();
        var harness = new Harness();

        var result = await harness.Engine.StartAsync(definition, WorkflowContext.Default);
        Assert.True(result.IsRunning);

        Assert.Empty(harness.Scheduler.DueTimers());
        Assert.Equal(0, await harness.Scheduler.FireDueAsync());

        harness.Clock.Advance(TimeSpan.FromMinutes(11));
        Assert.Single(harness.Scheduler.DueTimers());
        Assert.Equal(1, await harness.Scheduler.FireDueAsync());

        Assert.Equal(WorkflowStatus.Completed, harness.Engine.GetInstance(result.InstanceId)!.Status);
    }

    // ---- Wait signal ----------------------------------------------------------

    [Fact]
    public async Task A_wait_node_resumes_on_its_signal()
    {
        var definition = WorkflowDefinition.Create("wait", "Wait")
            .AddNode(new StartNode("s"))
            .AddNode(new WaitNode("hold", "go"))
            .AddNode(new EndNode("e"))
            .AddTransition("s", "hold")
            .AddTransition("hold", "e")
            .Build();
        var harness = new Harness();

        var result = await harness.Engine.StartAsync(definition, WorkflowContext.Default);
        Assert.True(result.IsRunning);

        Assert.False((await harness.Engine.SignalAsync(result.InstanceId, "other"))!.IsCompleted);
        Assert.True((await harness.Engine.SignalAsync(result.InstanceId, "go"))!.IsCompleted);
    }

    // ---- Cancellation ---------------------------------------------------------

    [Fact]
    public async Task A_running_instance_can_be_cancelled()
    {
        var harness = new Harness();
        var result = await harness.Engine.StartAsync(ApprovalFlow(), WorkflowContext.Default);
        Assert.True(result.IsRunning);

        var cancelled = await harness.Engine.CancelAsync(result.InstanceId);

        Assert.Equal(WorkflowStatus.Cancelled, cancelled!.Status);
        Assert.True(harness.Has<WorkflowCancelled>());

        // A cancelled instance no longer resumes.
        var afterCancel = await harness.Engine.CompleteActivityAsync(result.InstanceId, "review");
        Assert.Equal(WorkflowStatus.Cancelled, afterCancel!.Status);
    }

    // ---- Store & repository ---------------------------------------------------

    [Fact]
    public void Repository_returns_the_latest_version()
    {
        var repository = new InMemoryWorkflowRepository();
        repository.Register(WorkflowDefinition.Create("d", "D", WorkflowVersion.Initial)
            .AddNode(new StartNode("s")).AddNode(new EndNode("e")).AddTransition("s", "e").Build());
        repository.Register(WorkflowDefinition.Create("d", "D", new WorkflowVersion(2))
            .AddNode(new StartNode("s")).AddNode(new EndNode("e")).AddTransition("s", "e").Build());

        Assert.Equal(new WorkflowVersion(2), repository.GetLatest("d")!.Version);
        Assert.Equal(WorkflowVersion.Initial, repository.Get("d", WorkflowVersion.Initial)!.Version);
    }

    // ---- Dependency injection -------------------------------------------------

    [Fact]
    public void AddWorkflowEngine_registers_the_runtime()
    {
        var services = new ServiceCollection();
        services.AddWorkflowEngine();

        using var provider = services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });

        Assert.NotNull(provider.GetRequiredService<WorkflowEngine>());
        Assert.NotNull(provider.GetRequiredService<WorkflowExecutor>());
        Assert.NotNull(provider.GetRequiredService<WorkflowRuntime>());
        Assert.NotNull(provider.GetRequiredService<WorkflowScheduler>());
        Assert.IsType<InMemoryWorkflowRepository>(provider.GetRequiredService<IWorkflowRepository>());
        Assert.IsType<InMemoryWorkflowStore>(provider.GetRequiredService<IWorkflowStore>());
        Assert.IsType<SystemDateTimeProvider>(provider.GetRequiredService<IDateTimeProvider>());
    }
}
