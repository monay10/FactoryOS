using FactoryOS.Domain.Abstractions;
using FactoryOS.IntegrationTests.Persistence;
using FactoryOS.Plugins.Forms.Engine.Configuration;
using FactoryOS.Plugins.Forms.Engine.Domain;
using FactoryOS.Plugins.Forms.Engine.Execution;
using FactoryOS.Plugins.Workflow.Approvals.Configuration;
using FactoryOS.Plugins.Workflow.Approvals.Domain;
using FactoryOS.Plugins.Workflow.Approvals.Execution;
using FactoryOS.Plugins.Workflow.Engine.Domain;
using FactoryOS.Plugins.Workflow.Engine.Execution;
using FactoryOS.Plugins.Workflow.Engine.Nodes;
using FactoryOS.Plugins.Workflow.Tasks.Configuration;
using FactoryOS.Plugins.Workflow.Tasks.Domain;
using FactoryOS.Plugins.Workflow.Tasks.Execution;
using Microsoft.Extensions.DependencyInjection;
using WorkflowContext = FactoryOS.Plugins.Workflow.Engine.Configuration.WorkflowContext;

namespace FactoryOS.IntegrationTests.Workflow;

/// <summary>
/// The approval engine composed through <c>AddApprovalEngine</c> against a real container, driven together
/// with the stateful workflow (and, at the orchestration layer, the human task and forms) engines: a workflow
/// pauses on an activity, a bound approval routes it down the approval or rejection branch on completion, or
/// cancels it. Human task and forms integration are wired here at the orchestration layer — the approval
/// engine references neither.
/// </summary>
public sealed class ApprovalEngineIntegrationTests
{
    private static readonly DateTimeOffset Now = new(2026, 07, 21, 13, 00, 00, TimeSpan.Zero);

    private static ServiceProvider Build()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IDateTimeProvider>(new FixedClock(Now));
        services.AddApprovalEngine();
        services.AddHumanTaskEngine();
        services.AddFormsEngine();
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
    }

    private static WorkflowDefinition ApprovalWorkflow() => WorkflowDefinition.Create("appr-wf", "Approval Workflow")
        .AddNode(new StartNode("s"))
        .AddNode(new ActivityNode("approve", "Approve", "approve"))
        .AddNode(new DecisionNode("d"))
        .AddNode(new EndNode("granted"))
        .AddNode(new EndNode("denied"))
        .AddTransition("s", "approve")
        .AddTransition("approve", "d")
        .AddTransition("d", "granted", "approved == true")
        .AddTransition("d", "denied")
        .Build();

    private static ApprovalDefinition ManagerApproval() =>
        ApprovalDefinition.Create("mgr-approval", "Manager Approval")
            .ForActivity("approve")
            .AddSingle("mgr", ApprovalAssignment.User("manager"))
            .Build();

    [Fact]
    public void The_container_composes_the_approval_engine()
    {
        using var provider = Build();

        Assert.NotNull(provider.GetRequiredService<ApprovalEngine>());
        Assert.IsType<ApprovalWorkflowBridge>(provider.GetRequiredService<IApprovalWorkflowBridge>());
        Assert.NotNull(provider.GetRequiredService<WorkflowEngine>());
    }

    [Fact]
    public async Task An_approval_routes_the_workflow_down_the_granted_branch()
    {
        using var provider = Build();
        var workflow = provider.GetRequiredService<WorkflowEngine>();
        var approvals = provider.GetRequiredService<ApprovalEngine>();

        var run = await workflow.StartAsync(ApprovalWorkflow(), WorkflowContext.Default);
        Assert.True(workflow.GetInstance(run.InstanceId)!.PendingActivities.ContainsKey("approve"));

        var approval = await approvals.StartForActivityAsync(
            ManagerApproval(), ApprovalContext.Default, run.InstanceId, "approve");
        await approvals.ApproveAsync(approval.Id, "mgr", "manager");

        Assert.Equal(ApprovalStatus.Approved, approvals.GetApproval(approval.Id)!.Status);
        Assert.Equal(WorkflowStatus.Completed, workflow.GetInstance(run.InstanceId)!.Status);
        Assert.Contains(workflow.GetInstance(run.InstanceId)!.History.Entries, e => e.NodeId == "granted");
    }

    [Fact]
    public async Task A_rejection_routes_the_workflow_down_the_denied_branch()
    {
        using var provider = Build();
        var workflow = provider.GetRequiredService<WorkflowEngine>();
        var approvals = provider.GetRequiredService<ApprovalEngine>();

        var run = await workflow.StartAsync(ApprovalWorkflow(), WorkflowContext.Default);
        var approval = await approvals.StartForActivityAsync(
            ManagerApproval(), ApprovalContext.Default, run.InstanceId, "approve");

        await approvals.RejectAsync(approval.Id, "mgr", "manager", "over budget");

        Assert.Equal(ApprovalStatus.Rejected, approvals.GetApproval(approval.Id)!.Status);
        Assert.Equal(WorkflowStatus.Completed, workflow.GetInstance(run.InstanceId)!.Status);
        Assert.Contains(workflow.GetInstance(run.InstanceId)!.History.Entries, e => e.NodeId == "denied");
    }

    [Fact]
    public async Task Cancelling_a_bound_approval_cancels_the_workflow_instance()
    {
        using var provider = Build();
        var workflow = provider.GetRequiredService<WorkflowEngine>();
        var approvals = provider.GetRequiredService<ApprovalEngine>();

        var run = await workflow.StartAsync(ApprovalWorkflow(), WorkflowContext.Default);
        var approval = await approvals.StartForActivityAsync(
            ManagerApproval(), ApprovalContext.Default, run.InstanceId, "approve");

        await approvals.CancelAsync(approval.Id, "admin", "abandoned");

        Assert.Equal(ApprovalStatus.Cancelled, approvals.GetApproval(approval.Id)!.Status);
        Assert.Equal(WorkflowStatus.Cancelled, workflow.GetInstance(run.InstanceId)!.Status);
    }

    [Fact]
    public async Task Human_task_integration_is_composed_at_the_orchestration_layer()
    {
        using var provider = Build();
        var workflow = provider.GetRequiredService<WorkflowEngine>();
        var approvals = provider.GetRequiredService<ApprovalEngine>();
        var tasks = provider.GetRequiredService<HumanTaskEngine>();

        var run = await workflow.StartAsync(ApprovalWorkflow(), WorkflowContext.Default);
        var approval = await approvals.StartForActivityAsync(
            ManagerApproval(), ApprovalContext.Default, run.InstanceId, "approve");

        // The orchestration layer surfaces the approver's step as a human task (the approval engine never
        // references the task engine). Completing the task is what tells the orchestrator to record the vote.
        var task = await tasks.CreateAsync(
            HumanTaskDefinition.Create("approve-task", "Approve", HumanTaskAssignment.ToUser("manager")).Build(),
            HumanTaskContext.Default);
        await tasks.ApproveAsync(task.Id, "manager");
        Assert.Equal(HumanTaskStatus.Completed, tasks.GetTask(task.Id)!.Status);

        // ... and the orchestrator records the corresponding approval vote.
        await approvals.ApproveAsync(approval.Id, "mgr", "manager");

        Assert.Equal(ApprovalStatus.Approved, approvals.GetApproval(approval.Id)!.Status);
        Assert.Equal(WorkflowStatus.Completed, workflow.GetInstance(run.InstanceId)!.Status);
    }

    [Fact]
    public async Task Forms_integration_feeds_the_approval_context_at_the_orchestration_layer()
    {
        using var provider = Build();
        var approvals = provider.GetRequiredService<ApprovalEngine>();
        var forms = provider.GetRequiredService<FormEngine>();

        // The orchestration layer collects the request amount on a form, then starts an approval whose
        // auto-decision rule reads that amount. Small amounts auto-approve without asking anyone.
        var form = FormDefinition.Create("request-form", "Request")
            .AddSection(new FormSection("s", "Request",
            [
                new FormGroup("g", null,
                [
                    new FormField(new FieldDefinition("amount", "Amount", FieldType.Decimal)
                    {
                        Validation = new FieldValidation { Required = true, Min = 0m },
                    }),
                ]),
            ]))
            .Build();
        var formInstance = await forms.OpenAsync(form, FormContext.Default);
        var submission = await forms.SubmitAsync(formInstance.Id, new Dictionary<string, object?> { ["amount"] = 50m });
        Assert.True(submission!.IsAccepted);

        var def = ApprovalDefinition.Create("auto-approval", "Auto")
            .AddRule(new ApprovalRule("amount < 100", ApprovalOutcome.Approved))
            .AddSingle("mgr", ApprovalAssignment.User("manager"))
            .Build();
        var approval = await approvals.StartAsync(
            def, new ApprovalContext("default", values: submission.Submission!.Values));

        Assert.Equal(ApprovalStatus.Approved, approval.Status);
    }

    [Fact]
    public async Task Approval_state_and_history_are_persisted_and_readable()
    {
        using var provider = Build();
        var approvals = provider.GetRequiredService<ApprovalEngine>();

        var approval = await approvals.StartAsync(ManagerApproval(), ApprovalContext.Default);
        await approvals.AddCommentAsync(approval.Id, "manager", "reviewing");
        await approvals.ApproveAsync(approval.Id, "mgr", "manager");

        Assert.Equal(ApprovalStatus.Approved, approvals.GetApproval(approval.Id)!.Status);
        var actions = approvals.GetHistory(approval.Id).Select(entry => entry.Action).ToArray();
        Assert.Contains("commented", actions);
        Assert.Contains("approved", actions);
    }
}
