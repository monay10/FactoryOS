using FactoryOS.Plugins.Workflow.Approvals.Configuration;
using FactoryOS.Plugins.Workflow.Approvals.Diagnostics;
using FactoryOS.Plugins.Workflow.Approvals.Domain;
using FactoryOS.Plugins.Workflow.Approvals.Events;
using FactoryOS.Plugins.Workflow.Approvals.Execution;
using FactoryOS.Plugins.Workflow.Approvals.Persistence;
using FactoryOS.Plugins.Workflow.Approvals.Policies;
using FactoryOS.Tests.Identity;

namespace FactoryOS.Tests.Workflow.Approvals;

/// <summary>
/// Unit coverage of the approval engine core: every decision policy (single, any, all, majority, consensus,
/// weighted, percentage, first response), sequential and parallel structure, auto-decision rules, reminders,
/// escalation and expiry, participant assignment, permissions and history — exercised directly, without a
/// container and without a workflow. Workflow branch routing is proven in the integration suite.
/// </summary>
public sealed class ApprovalEngineCoreTests
{
    private static readonly DateTimeOffset Now = new(2026, 07, 21, 09, 00, 00, TimeSpan.Zero);

    private sealed class Harness
    {
        public Harness()
        {
            Clock = new MutableClock(Now);
            Store = new InMemoryApprovalStore();
            Events = new InMemoryApprovalEventSink();
            Metrics = new ApprovalMetrics();
            var repository = new InMemoryApprovalRepository();
            var history = new InMemoryApprovalHistoryRepository();
            var executor = new ApprovalExecutor();
            var resolver = new ParticipantResolver();
            var evaluator = new ApprovalPolicyEvaluator();
            var options = new ApprovalEngineOptions();
            var completion = new ApprovalCompletionService(Store, history, Events, executor, Metrics, Clock);
            var runtime = new ApprovalRuntime(
                repository, Store, history, Events, executor, resolver,
                new ApprovalDeadlineEngine(), new ApprovalReminderEngine(), new ApprovalEscalationEngine(),
                completion, Metrics, options, Clock);
            var decision = new ApprovalDecisionService(
                Store, repository, history, Events, executor, evaluator, runtime, completion, Metrics, Clock);
            var cancellation = new ApprovalCancellationService(Store, history, Events, executor, Metrics, Clock);
            Engine = new ApprovalEngine(runtime, decision, cancellation, Store, history);
        }

        public ApprovalEngine Engine { get; }

        public MutableClock Clock { get; }

        public InMemoryApprovalStore Store { get; }

        public InMemoryApprovalEventSink Events { get; }

        public ApprovalMetrics Metrics { get; }
    }

    private static ApprovalParticipant P(string id, string user, int weight = 1) =>
        new(id, ApprovalAssignment.User(user), weight);

    // ---- Single ----

    [Fact]
    public async Task Single_approval_completes_on_the_one_vote()
    {
        var harness = new Harness();
        var def = ApprovalDefinition.Create("single", "Single").AddSingle("m", ApprovalAssignment.User("u1")).Build();
        var approval = await harness.Engine.StartAsync(def, ApprovalContext.Default);

        Assert.Equal(ApprovalStructure.Single, def.Structure);
        var result = await harness.Engine.ApproveAsync(approval.Id, "m", "u1");

        Assert.Equal(ApprovalStatus.Approved, result!.Status);
        Assert.Equal(ApprovalOutcome.Approved, result.Outcome);
        Assert.Contains(harness.Events.Events, e => e is ApprovalCompleted { Approved: true });
    }

    // ---- Parallel: any / all ----

    [Fact]
    public async Task Any_approver_completes_on_the_first_approval()
    {
        var harness = new Harness();
        var def = ApprovalDefinition.Create("any", "Any")
            .AddStage("s", ApprovalPolicies.AnyApprover, [P("a", "u1"), P("b", "u2")]).Build();
        var approval = await harness.Engine.StartAsync(def, ApprovalContext.Default);

        var result = await harness.Engine.ApproveAsync(approval.Id, "a", "u1");

        Assert.Equal(ApprovalStatus.Approved, result!.Status);
    }

    [Fact]
    public async Task All_approvers_needs_every_vote()
    {
        var harness = new Harness();
        var def = ApprovalDefinition.Create("all", "All")
            .AddStage("s", ApprovalPolicies.AllApprovers, [P("a", "u1"), P("b", "u2")]).Build();
        var approval = await harness.Engine.StartAsync(def, ApprovalContext.Default);

        await harness.Engine.ApproveAsync(approval.Id, "a", "u1");
        Assert.Equal(ApprovalStatus.InProgress, harness.Engine.GetApproval(approval.Id)!.Status);

        var result = await harness.Engine.ApproveAsync(approval.Id, "b", "u2");
        Assert.Equal(ApprovalStatus.Approved, result!.Status);
    }

    [Fact]
    public async Task All_approvers_rejects_as_soon_as_one_rejects()
    {
        var harness = new Harness();
        var def = ApprovalDefinition.Create("all", "All")
            .AddStage("s", ApprovalPolicies.AllApprovers, [P("a", "u1"), P("b", "u2")]).Build();
        var approval = await harness.Engine.StartAsync(def, ApprovalContext.Default);

        var result = await harness.Engine.RejectAsync(approval.Id, "a", "u1");

        Assert.Equal(ApprovalStatus.Rejected, result!.Status);
    }

    // ---- Majority ----

    [Fact]
    public async Task Majority_completes_when_more_than_half_approve()
    {
        var harness = new Harness();
        var def = ApprovalDefinition.Create("maj", "Majority")
            .AddStage("s", ApprovalPolicies.Majority, [P("a", "u1"), P("b", "u2"), P("c", "u3")]).Build();
        var approval = await harness.Engine.StartAsync(def, ApprovalContext.Default);

        await harness.Engine.ApproveAsync(approval.Id, "a", "u1");
        Assert.Equal(ApprovalStatus.InProgress, harness.Engine.GetApproval(approval.Id)!.Status);
        var result = await harness.Engine.ApproveAsync(approval.Id, "b", "u2");

        Assert.Equal(ApprovalStatus.Approved, result!.Status);
    }

    [Fact]
    public async Task Majority_rejects_when_approval_becomes_impossible()
    {
        var harness = new Harness();
        var def = ApprovalDefinition.Create("maj", "Majority")
            .AddStage("s", ApprovalPolicies.Majority, [P("a", "u1"), P("b", "u2"), P("c", "u3")]).Build();
        var approval = await harness.Engine.StartAsync(def, ApprovalContext.Default);

        await harness.Engine.RejectAsync(approval.Id, "a", "u1");
        var result = await harness.Engine.RejectAsync(approval.Id, "b", "u2");

        Assert.Equal(ApprovalStatus.Rejected, result!.Status);
    }

    // ---- Consensus / weighted / percentage / first response ----

    [Fact]
    public async Task Consensus_rejects_on_any_dissent()
    {
        var harness = new Harness();
        var def = ApprovalDefinition.Create("con", "Consensus")
            .AddStage("s", ApprovalPolicies.Consensus, [P("a", "u1"), P("b", "u2")]).Build();
        var approval = await harness.Engine.StartAsync(def, ApprovalContext.Default);

        await harness.Engine.ApproveAsync(approval.Id, "a", "u1");
        var result = await harness.Engine.RejectAsync(approval.Id, "b", "u2");

        Assert.Equal(ApprovalStatus.Rejected, result!.Status);
    }

    [Fact]
    public async Task Weighted_vote_completes_when_the_weight_threshold_is_met()
    {
        var harness = new Harness();
        var def = ApprovalDefinition.Create("w", "Weighted")
            .AddStage("s", ApprovalPolicies.Weighted(3), [P("a", "u1", weight: 3), P("b", "u2", weight: 1)]).Build();
        var approval = await harness.Engine.StartAsync(def, ApprovalContext.Default);

        var result = await harness.Engine.ApproveAsync(approval.Id, "a", "u1");

        Assert.Equal(ApprovalStatus.Approved, result!.Status);
    }

    [Fact]
    public async Task Percentage_completes_when_the_share_is_reached()
    {
        var harness = new Harness();
        var def = ApprovalDefinition.Create("p", "Percentage")
            .AddStage("s", ApprovalPolicies.Percentage(50), [P("a", "u1"), P("b", "u2"), P("c", "u3"), P("d", "u4")])
            .Build();
        var approval = await harness.Engine.StartAsync(def, ApprovalContext.Default);

        await harness.Engine.ApproveAsync(approval.Id, "a", "u1");
        var result = await harness.Engine.ApproveAsync(approval.Id, "b", "u2");

        Assert.Equal(ApprovalStatus.Approved, result!.Status);
    }

    [Fact]
    public async Task First_response_lets_the_first_vote_decide()
    {
        var harness = new Harness();
        var def = ApprovalDefinition.Create("fr", "FirstResponse")
            .AddStage("s", ApprovalPolicies.FirstResponse, [P("a", "u1"), P("b", "u2")]).Build();
        var approval = await harness.Engine.StartAsync(def, ApprovalContext.Default);

        var result = await harness.Engine.RejectAsync(approval.Id, "a", "u1");

        Assert.Equal(ApprovalStatus.Rejected, result!.Status);
    }

    // ---- Sequential ----

    [Fact]
    public async Task Sequential_advances_stage_by_stage_and_short_circuits_on_rejection()
    {
        var harness = new Harness();
        var def = ApprovalDefinition.Create("seq", "Sequential")
            .AddSingle("mgr", ApprovalAssignment.User("u1"))
            .AddSingle("dir", ApprovalAssignment.User("u2"))
            .Build();
        Assert.Equal(ApprovalStructure.Sequential, def.Structure);

        var approval = await harness.Engine.StartAsync(def, ApprovalContext.Default);
        Assert.Equal(1, harness.Engine.GetApproval(approval.Id)!.CurrentLevel.Value);

        await harness.Engine.ApproveAsync(approval.Id, "mgr", "u1");
        var afterFirst = harness.Engine.GetApproval(approval.Id)!;
        Assert.Equal(ApprovalStatus.InProgress, afterFirst.Status);
        Assert.Equal(2, afterFirst.CurrentLevel.Value); // advanced to the second stage

        var result = await harness.Engine.ApproveAsync(approval.Id, "dir", "u2");
        Assert.Equal(ApprovalStatus.Approved, result!.Status);
    }

    [Fact]
    public async Task Sequential_rejection_at_the_first_stage_stops_the_approval()
    {
        var harness = new Harness();
        var def = ApprovalDefinition.Create("seq", "Sequential")
            .AddSingle("mgr", ApprovalAssignment.User("u1"))
            .AddSingle("dir", ApprovalAssignment.User("u2"))
            .Build();
        var approval = await harness.Engine.StartAsync(def, ApprovalContext.Default);

        var result = await harness.Engine.RejectAsync(approval.Id, "mgr", "u1");

        Assert.Equal(ApprovalStatus.Rejected, result!.Status);
        Assert.Equal(1, result.CurrentLevel.Value); // never advanced
    }

    // ---- Auto-decision rules ----

    [Fact]
    public async Task An_auto_rule_short_circuits_the_approval()
    {
        var harness = new Harness();
        var def = ApprovalDefinition.Create("auto", "Auto")
            .AddRule(new ApprovalRule("amount < 100", ApprovalOutcome.Approved))
            .AddSingle("m", ApprovalAssignment.User("u1"))
            .Build();

        var approval = await harness.Engine.StartAsync(
            def, new ApprovalContext("default", values: new Dictionary<string, object?> { ["amount"] = 50m }));

        Assert.Equal(ApprovalStatus.Approved, approval.Status);
        Assert.Empty(approval.Steps); // no participant was ever asked
    }

    // ---- Assignment ----

    [Fact]
    public async Task A_dynamic_participant_is_resolved_from_context_values()
    {
        var harness = new Harness();
        var def = ApprovalDefinition.Create("dyn", "Dynamic")
            .AddSingle("approver", ApprovalAssignment.Dynamic("approver"))
            .Build();

        var approval = await harness.Engine.StartAsync(
            def, new ApprovalContext("default", values: new Dictionary<string, object?> { ["approver"] = "boss" }));

        Assert.Equal("boss", approval.ActiveStageSteps[0].Assignee);
    }

    // ---- Reminder / escalation / expiry ----

    private static ApprovalDefinitionBuilder Timed() =>
        ApprovalDefinition.Create("timed", "Timed").AddSingle("m", ApprovalAssignment.User("u1"));

    [Fact]
    public async Task A_reminder_fires_once_when_due()
    {
        var harness = new Harness();
        var def = Timed().WithDeadline(ApprovalDeadline.In(TimeSpan.FromHours(2)))
            .AddReminder(new ApprovalReminder(TimeSpan.FromHours(1))).Build();
        await harness.Engine.StartAsync(def, ApprovalContext.Default);

        Assert.Equal(0, (await harness.Engine.RunDueAsync()).RemindersFired);
        harness.Clock.Advance(TimeSpan.FromHours(1));
        Assert.Equal(1, (await harness.Engine.RunDueAsync()).RemindersFired);
        Assert.Equal(0, (await harness.Engine.RunDueAsync()).RemindersFired);
        Assert.Contains(harness.Events.Events, e => e is ApprovalReminderSent);
    }

    [Fact]
    public async Task An_approval_escalates_to_its_target_after_the_deadline()
    {
        var harness = new Harness();
        var def = Timed().WithDeadline(ApprovalDeadline.In(TimeSpan.FromHours(1)))
            .AddEscalation(new ApprovalEscalation(TimeSpan.Zero, ApprovalAssignment.User("boss"))).Build();
        var approval = await harness.Engine.StartAsync(def, ApprovalContext.Default);

        harness.Clock.Advance(TimeSpan.FromHours(1));
        var summary = await harness.Engine.RunDueAsync();

        Assert.Equal(1, summary.EscalationsApplied);
        var escalated = harness.Engine.GetApproval(approval.Id)!;
        Assert.Equal(ApprovalStatus.InProgress, escalated.Status); // escalated, not expired
        Assert.Equal("boss", escalated.ActiveStageSteps[0].Assignee);
        Assert.Contains(harness.Events.Events, e => e is ApprovalEscalated);
    }

    [Fact]
    public async Task An_approval_with_no_escalation_expires_after_the_deadline()
    {
        var harness = new Harness();
        var def = Timed().WithDeadline(ApprovalDeadline.In(TimeSpan.FromHours(1))).Build();
        var approval = await harness.Engine.StartAsync(def, ApprovalContext.Default);

        harness.Clock.Advance(TimeSpan.FromHours(1));
        var summary = await harness.Engine.RunDueAsync();

        Assert.Equal(1, summary.ApprovalsExpired);
        Assert.Equal(ApprovalStatus.Expired, harness.Engine.GetApproval(approval.Id)!.Status);
        Assert.Contains(harness.Events.Events, e => e is ApprovalExpired);
    }

    // ---- Cancellation ----

    [Fact]
    public async Task An_approval_can_be_cancelled()
    {
        var harness = new Harness();
        var def = ApprovalDefinition.Create("c", "Cancel").AddSingle("m", ApprovalAssignment.User("u1")).Build();
        var approval = await harness.Engine.StartAsync(def, ApprovalContext.Default);

        var cancelled = await harness.Engine.CancelAsync(approval.Id, "admin", "withdrawn");

        Assert.Equal(ApprovalStatus.Cancelled, cancelled!.Status);
        Assert.Contains(harness.Events.Events, e => e is ApprovalCancelled);
    }

    [Fact]
    public async Task Each_terminal_disposition_keeps_a_distinct_resolution()
    {
        var harness = new Harness();

        static ApprovalDefinition Def(string key) =>
            ApprovalDefinition.Create(key, key)
                .AddSingle("m", ApprovalAssignment.User("u1"))
                .WithDeadline(ApprovalDeadline.In(TimeSpan.FromHours(1)))
                .Build();

        var approvedApproval = await harness.Engine.StartAsync(Def("a"), ApprovalContext.Default);
        await harness.Engine.ApproveAsync(approvedApproval.Id, "m", "u1");
        Assert.Equal(ApprovalResolution.Approved, harness.Engine.GetApproval(approvedApproval.Id)!.Resolution);

        var rejectedApproval = await harness.Engine.StartAsync(Def("r"), ApprovalContext.Default);
        await harness.Engine.RejectAsync(rejectedApproval.Id, "m", "u1");
        Assert.Equal(ApprovalResolution.Rejected, harness.Engine.GetApproval(rejectedApproval.Id)!.Resolution);

        var cancelledApproval = await harness.Engine.StartAsync(Def("c"), ApprovalContext.Default);
        await harness.Engine.CancelAsync(cancelledApproval.Id);
        Assert.Equal(ApprovalResolution.Cancelled, harness.Engine.GetApproval(cancelledApproval.Id)!.Resolution);

        var expiredApproval = await harness.Engine.StartAsync(Def("e"), ApprovalContext.Default);
        harness.Clock.Advance(TimeSpan.FromHours(1));
        await harness.Engine.RunDueAsync();
        Assert.Equal(ApprovalResolution.Expired, harness.Engine.GetApproval(expiredApproval.Id)!.Resolution);
    }

    // ---- Permissions ----

    [Fact]
    public async Task The_active_approver_holds_implicit_approve_and_reject_rights()
    {
        var harness = new Harness();
        var def = ApprovalDefinition.Create("perm", "Perm")
            .AddSingle("m", ApprovalAssignment.User("u1"))
            .AddPermission(new ApprovalPermissionGrant(ApprovalPrincipalKind.Role, "admin", ApprovalPermission.Cancel))
            .Build();
        var approval = await harness.Engine.StartAsync(def, ApprovalContext.Default);
        var evaluator = new ApprovalPermissionEvaluator();

        Assert.True(evaluator.HasPermission(def, approval, ApprovalPrincipal.ForUser("u1"),
            ApprovalPermission.Approve | ApprovalPermission.Reject));
        Assert.False(evaluator.HasPermission(def, approval, ApprovalPrincipal.ForUser("u1"), ApprovalPermission.Cancel));
        Assert.True(evaluator.HasPermission(def, approval, new ApprovalPrincipal("x", ["admin"], []),
            ApprovalPermission.Cancel));
    }

    // ---- History ----

    [Fact]
    public async Task History_records_the_lifecycle_in_order()
    {
        var harness = new Harness();
        var def = ApprovalDefinition.Create("h", "History").AddSingle("m", ApprovalAssignment.User("u1")).Build();
        var approval = await harness.Engine.StartAsync(def, ApprovalContext.Default);
        await harness.Engine.ApproveAsync(approval.Id, "m", "u1");

        var actions = harness.Engine.GetHistory(approval.Id).Select(entry => entry.Action).ToArray();

        Assert.Equal(["created", "started", "stage-activated", "voted:Approve", "approved"], actions);
    }
}
