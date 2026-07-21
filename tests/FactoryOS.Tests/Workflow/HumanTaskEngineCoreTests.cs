using FactoryOS.Plugins.Workflow.Tasks.Configuration;
using FactoryOS.Plugins.Workflow.Tasks.Diagnostics;
using FactoryOS.Plugins.Workflow.Tasks.Domain;
using FactoryOS.Plugins.Workflow.Tasks.Events;
using FactoryOS.Plugins.Workflow.Tasks.Execution;
using FactoryOS.Plugins.Workflow.Tasks.Persistence;
using FactoryOS.Tests.Identity;

namespace FactoryOS.Tests.Workflow.Tasks;

/// <summary>
/// Unit coverage of the human task engine core: assignment resolution (every strategy), the task lifecycle,
/// reminders, escalation and expiry, permissions, comments, attachments and history — exercised directly,
/// without a container and without a workflow. Workflow resumption is proven in the integration suite.
/// </summary>
public sealed class HumanTaskEngineCoreTests
{
    private static readonly DateTimeOffset Now = new(2026, 07, 21, 08, 00, 00, TimeSpan.Zero);

    private sealed class Harness
    {
        public Harness()
        {
            Clock = new MutableClock(Now);
            Store = new InMemoryHumanTaskStore();
            Directory = new InMemoryHumanTaskDirectory();
            Events = new InMemoryHumanTaskEventSink();
            Metrics = new HumanTaskMetrics();
            var repository = new InMemoryHumanTaskRepository();
            var history = new InMemoryHumanTaskHistoryRepository();
            var executor = new HumanTaskExecutor();
            Resolver = new AssignmentResolver(Directory, Store);
            var options = new HumanTaskEngineOptions();
            var runtime = new HumanTaskRuntime(
                repository, Store, history, Events, executor, Resolver,
                new DeadlineEngine(), new ReminderEngine(), new EscalationEngine(), Metrics, options, Clock);
            var completion = new TaskCompletionService(Store, history, Events, executor, Metrics, Clock);
            var cancellation = new TaskCancellationService(Store, history, Events, executor, Metrics, Clock);
            Engine = new HumanTaskEngine(runtime, completion, cancellation, Store, history);
        }

        public HumanTaskEngine Engine { get; }

        public MutableClock Clock { get; }

        public InMemoryHumanTaskStore Store { get; }

        public InMemoryHumanTaskDirectory Directory { get; }

        public InMemoryHumanTaskEventSink Events { get; }

        public HumanTaskMetrics Metrics { get; }

        public AssignmentResolver Resolver { get; }
    }

    private static HumanTaskDefinition Simple(HumanTaskAssignment? assignment = null) =>
        HumanTaskDefinition.Create("review", "Review", assignment ?? HumanTaskAssignment.ToUser("u1"))
            .WithTitle("Review request")
            .OfCategory(HumanTaskCategory.Review)
            .Build();

    // ---- Assignment resolution ----

    [Fact]
    public void A_user_assignment_resolves_to_that_user()
    {
        var outcome = new Harness().Resolver.Resolve(HumanTaskAssignment.ToUser("alice"));
        Assert.Equal("alice", outcome.Assignee);
    }

    [Fact]
    public void A_dynamic_assignment_resolves_from_an_expression()
    {
        var outcome = new Harness().Resolver.Resolve(
            HumanTaskAssignment.ToExpression("manager"),
            new Dictionary<string, object?> { ["manager"] = "boss" });
        Assert.Equal("boss", outcome.Assignee);
    }

    [Fact]
    public void A_role_assignment_resolves_to_a_claimable_pool_of_members()
    {
        var harness = new Harness();
        harness.Directory.AddToRole("approver", "a1");
        harness.Directory.AddToRole("approver", "a2");

        var outcome = harness.Resolver.Resolve(HumanTaskAssignment.ToRole("approver"));

        Assert.Null(outcome.Assignee);
        Assert.Contains("a1", outcome.Candidates);
        Assert.Contains("a2", outcome.Candidates);
    }

    [Fact]
    public void A_round_robin_assignment_rotates_through_the_pool()
    {
        var resolver = new Harness().Resolver;
        var pool = HumanTaskAssignment.RoundRobin("a", "b", "c");

        Assert.Equal("a", resolver.Resolve(pool).Assignee);
        Assert.Equal("b", resolver.Resolve(pool).Assignee);
        Assert.Equal("c", resolver.Resolve(pool).Assignee);
        Assert.Equal("a", resolver.Resolve(pool).Assignee);
    }

    [Fact]
    public void A_load_balanced_assignment_picks_the_least_loaded_candidate()
    {
        var harness = new Harness();
        // Give 'a' two open tasks so 'b' is lighter.
        for (var i = 0; i < 2; i++)
        {
            var busy = HumanTaskInstance.Create(
                Guid.NewGuid(), "x", "default", "t", HumanTaskCategory.General, HumanTaskPriority.Normal);
            busy.AssignTo("a");
            harness.Store.Save(busy);
        }

        var outcome = harness.Resolver.Resolve(HumanTaskAssignment.LoadBalanced("a", "b"));

        Assert.Equal("b", outcome.Assignee);
    }

    // ---- Task lifecycle ----

    [Fact]
    public async Task Creating_a_task_assigns_it_and_moves_it_to_waiting()
    {
        var harness = new Harness();
        var task = await harness.Engine.CreateAsync(Simple(), HumanTaskContext.Default);

        Assert.Equal(HumanTaskStatus.Waiting, task.Status);
        Assert.Equal("u1", task.Assignee);
        Assert.Contains(harness.Events.Events, e => e is HumanTaskCreated);
        Assert.Contains(harness.Events.Events, e => e is HumanTaskAssigned);
    }

    [Fact]
    public async Task Opening_then_completing_a_task_runs_the_lifecycle()
    {
        var harness = new Harness();
        var task = await harness.Engine.CreateAsync(Simple(), HumanTaskContext.Default);

        await harness.Engine.OpenAsync(task.Id, "u1");
        Assert.Equal(HumanTaskStatus.InProgress, harness.Engine.GetTask(task.Id)!.Status);

        var completed = await harness.Engine.ApproveAsync(task.Id, "u1");
        Assert.Equal(HumanTaskStatus.Completed, completed!.Status);
        Assert.Equal(HumanTaskOutcome.Approved, completed.Decision!.Outcome);
        Assert.Contains(harness.Events.Events, e => e is HumanTaskCompleted);
    }

    [Fact]
    public async Task A_task_can_be_rejected()
    {
        var harness = new Harness();
        var task = await harness.Engine.CreateAsync(Simple(), HumanTaskContext.Default);

        var rejected = await harness.Engine.RejectAsync(task.Id, "u1", "not enough detail");

        Assert.Equal(HumanTaskStatus.Rejected, rejected!.Status);
        Assert.Contains(harness.Events.Events, e => e is HumanTaskRejected);
    }

    [Fact]
    public async Task A_task_can_be_cancelled()
    {
        var harness = new Harness();
        var task = await harness.Engine.CreateAsync(Simple(), HumanTaskContext.Default);

        var cancelled = await harness.Engine.CancelAsync(task.Id, "admin", "no longer needed");

        Assert.Equal(HumanTaskStatus.Cancelled, cancelled!.Status);
        Assert.Contains(harness.Events.Events, e => e is HumanTaskCancelled);
    }

    [Fact]
    public async Task A_task_can_be_reassigned()
    {
        var harness = new Harness();
        var task = await harness.Engine.CreateAsync(Simple(), HumanTaskContext.Default);

        var reassigned = await harness.Engine.ReassignAsync(task.Id, "u2", "admin");

        Assert.Equal("u2", reassigned!.Assignee);
        Assert.Contains(harness.Events.Events, e => e is HumanTaskReassigned);
    }

    [Fact]
    public async Task A_finished_task_cannot_change()
    {
        var harness = new Harness();
        var task = await harness.Engine.CreateAsync(Simple(), HumanTaskContext.Default);
        await harness.Engine.ApproveAsync(task.Id, "u1");

        await Assert.ThrowsAsync<InvalidOperationException>(() => harness.Engine.OpenAsync(task.Id, "u1"));
    }

    // ---- Reminder / escalation / expiry ----

    private static HumanTaskDefinition WithDeadline(
        TimeSpan deadline,
        TimeSpan? reminderBefore = null,
        (TimeSpan After, string To)? escalation = null)
    {
        var builder = HumanTaskDefinition.Create("timed", "Timed", HumanTaskAssignment.ToUser("u1"))
            .WithDeadline(HumanTaskDeadline.In(deadline));
        if (reminderBefore is TimeSpan before)
        {
            builder.AddReminder(new HumanTaskReminder(before));
        }

        if (escalation is { } esc)
        {
            builder.AddEscalation(new HumanTaskEscalation(esc.After, HumanTaskAssignment.ToUser(esc.To)));
        }

        return builder.Build();
    }

    [Fact]
    public async Task A_reminder_fires_once_when_it_comes_due()
    {
        var harness = new Harness();
        await harness.Engine.CreateAsync(WithDeadline(TimeSpan.FromHours(2), reminderBefore: TimeSpan.FromHours(1)),
            HumanTaskContext.Default);

        Assert.Equal(0, (await harness.Engine.RunDueAsync()).RemindersFired);

        harness.Clock.Advance(TimeSpan.FromHours(1)); // reminder fires 1h before the 2h deadline
        Assert.Equal(1, (await harness.Engine.RunDueAsync()).RemindersFired);
        Assert.Equal(0, (await harness.Engine.RunDueAsync()).RemindersFired); // idempotent
        Assert.Equal(1, harness.Metrics.Snapshot().Reminders);
    }

    [Fact]
    public async Task A_task_escalates_to_its_target_after_the_deadline()
    {
        var harness = new Harness();
        var task = await harness.Engine.CreateAsync(
            WithDeadline(TimeSpan.FromHours(1), escalation: (TimeSpan.Zero, "boss")), HumanTaskContext.Default);

        harness.Clock.Advance(TimeSpan.FromHours(1));
        var summary = await harness.Engine.RunDueAsync();

        Assert.Equal(1, summary.EscalationsApplied);
        var escalated = harness.Engine.GetTask(task.Id)!;
        Assert.Equal(HumanTaskStatus.Escalated, escalated.Status);
        Assert.Equal("boss", escalated.Assignee);
        Assert.Equal(1, escalated.EscalationLevel);
        Assert.Contains(harness.Events.Events, e => e is HumanTaskEscalated);
    }

    [Fact]
    public async Task A_task_with_no_escalation_expires_after_the_deadline()
    {
        var harness = new Harness();
        var task = await harness.Engine.CreateAsync(WithDeadline(TimeSpan.FromHours(1)), HumanTaskContext.Default);

        harness.Clock.Advance(TimeSpan.FromHours(1));
        var summary = await harness.Engine.RunDueAsync();

        Assert.Equal(1, summary.TasksExpired);
        Assert.Equal(HumanTaskStatus.Expired, harness.Engine.GetTask(task.Id)!.Status);
        Assert.Contains(harness.Events.Events, e => e is HumanTaskExpired);
    }

    // ---- Permissions ----

    [Fact]
    public void The_assignee_holds_implicit_complete_and_reject_rights()
    {
        var definition = Simple(HumanTaskAssignment.ToUser("u1"));
        var instance = HumanTaskInstance.Create(
            Guid.NewGuid(), definition.Key, "default", definition.Title, definition.Category, definition.Priority);
        instance.AssignTo("u1");
        var evaluator = new HumanTaskPermissionEvaluator();

        Assert.True(evaluator.HasPermission(definition, instance, HumanTaskPrincipal.ForUser("u1"),
            HumanTaskPermission.Complete | HumanTaskPermission.Reject));
        Assert.False(evaluator.HasPermission(definition, instance, HumanTaskPrincipal.ForUser("u1"),
            HumanTaskPermission.Cancel));
    }

    [Fact]
    public void A_role_grant_confers_its_permissions()
    {
        var definition = HumanTaskDefinition.Create("t", "T", HumanTaskAssignment.ToUser("u1"))
            .AddPermission(new HumanTaskPermissionGrant(
                HumanTaskPrincipalKind.Role, "supervisor", HumanTaskPermission.Cancel | HumanTaskPermission.Reassign))
            .Build();
        var instance = HumanTaskInstance.Create(
            Guid.NewGuid(), definition.Key, "default", definition.Title, definition.Category, definition.Priority);
        var evaluator = new HumanTaskPermissionEvaluator();
        var supervisor = new HumanTaskPrincipal("s1", ["supervisor"], []);

        Assert.True(evaluator.HasPermission(definition, instance, supervisor, HumanTaskPermission.Cancel));
        Assert.False(evaluator.HasPermission(definition, instance, supervisor, HumanTaskPermission.Complete));
    }

    // ---- Comments / attachments / history ----

    [Fact]
    public async Task Comments_and_attachments_are_recorded()
    {
        var harness = new Harness();
        var task = await harness.Engine.CreateAsync(Simple(), HumanTaskContext.Default);

        await harness.Engine.AddCommentAsync(task.Id, "u1", "looking into it", CommentVisibility.Internal);
        await harness.Engine.AddAttachmentAsync(task.Id, "photo.jpg", "minio://bucket/photo.jpg", "image/jpeg", 1024, "u1");

        var reloaded = harness.Engine.GetTask(task.Id)!;
        Assert.Single(reloaded.Comments);
        Assert.Equal(CommentVisibility.Internal, reloaded.Comments[0].Visibility);
        Assert.Single(reloaded.Attachments);
        Assert.Equal("minio://bucket/photo.jpg", reloaded.Attachments[0].StorageKey);
    }

    [Fact]
    public async Task History_is_kept_in_order()
    {
        var harness = new Harness();
        var task = await harness.Engine.CreateAsync(Simple(), HumanTaskContext.Default);
        await harness.Engine.OpenAsync(task.Id, "u1");
        await harness.Engine.ApproveAsync(task.Id, "u1");

        var actions = harness.Engine.GetHistory(task.Id).Select(entry => entry.Action).ToArray();

        Assert.Equal(["created", "assigned", "opened", "completed"], actions);
    }

    [Fact]
    public async Task Tasks_can_be_listed_by_assignee_as_summaries()
    {
        var harness = new Harness();
        await harness.Engine.CreateAsync(Simple(HumanTaskAssignment.ToUser("u1")), HumanTaskContext.Default);

        var mine = harness.Engine.ListByAssignee("u1");

        Assert.Single(mine);
        Assert.Equal("Review request", mine[0].Title);
    }
}
