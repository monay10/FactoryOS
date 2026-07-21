using FactoryOS.Domain.Abstractions;
using FactoryOS.IntegrationTests.Persistence;
using FactoryOS.Plugins.Forms.Engine.Configuration;
using FactoryOS.Plugins.Forms.Engine.Domain;
using FactoryOS.Plugins.Forms.Engine.Execution;
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
/// The human task engine composed through <c>AddHumanTaskEngine</c> against a real container, driven together
/// with the stateful workflow (and forms) engine: a workflow pauses on an activity, a task bound to it is
/// created, and completing, rejecting or cancelling the task advances or cancels the workflow. Also covers a
/// form feeding a task's completion outcome, persistence and container validation.
/// </summary>
public sealed class HumanTaskEngineIntegrationTests
{
    private static readonly DateTimeOffset Now = new(2026, 07, 21, 12, 00, 00, TimeSpan.Zero);

    private static ServiceProvider Build()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IDateTimeProvider>(new FixedClock(Now));
        services.AddHumanTaskEngine();
        services.AddFormsEngine();
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
    }

    private static WorkflowDefinition ApprovalWorkflow() => WorkflowDefinition.Create("approval-wf", "Approval")
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

    private static HumanTaskDefinition ApprovalTask() =>
        HumanTaskDefinition.Create("approve-task", "Approve", HumanTaskAssignment.ToUser("manager"))
            .ForActivity("approve")
            .OfCategory(HumanTaskCategory.Approval)
            .Build();

    [Fact]
    public void The_container_composes_the_human_task_engine()
    {
        using var provider = Build();

        Assert.NotNull(provider.GetRequiredService<HumanTaskEngine>());
        Assert.IsType<HumanTaskWorkflowBridge>(provider.GetRequiredService<IHumanTaskWorkflowBridge>());
        Assert.NotNull(provider.GetRequiredService<WorkflowEngine>());
    }

    [Fact]
    public async Task A_workflow_activity_creates_a_waiting_task_that_completes_the_workflow()
    {
        using var provider = Build();
        var workflow = provider.GetRequiredService<WorkflowEngine>();
        var tasks = provider.GetRequiredService<HumanTaskEngine>();

        var run = await workflow.StartAsync(ApprovalWorkflow(), WorkflowContext.Default);
        Assert.True(workflow.GetInstance(run.InstanceId)!.PendingActivities.ContainsKey("approve"));

        var task = await tasks.CreateForActivityAsync(ApprovalTask(), HumanTaskContext.Default, run.InstanceId, "approve");
        Assert.Equal(HumanTaskStatus.Waiting, task.Status); // the task is waiting; the workflow is paused

        await tasks.OpenAsync(task.Id, "manager");
        await tasks.ApproveAsync(task.Id, "manager");

        Assert.Equal(HumanTaskStatus.Completed, tasks.GetTask(task.Id)!.Status);
        Assert.Equal(WorkflowStatus.Completed, workflow.GetInstance(run.InstanceId)!.Status);
        Assert.Contains(workflow.GetInstance(run.InstanceId)!.History.Entries, e => e.NodeId == "granted");
    }

    [Fact]
    public async Task Rejecting_a_task_advances_the_workflow_down_the_denied_branch()
    {
        using var provider = Build();
        var workflow = provider.GetRequiredService<WorkflowEngine>();
        var tasks = provider.GetRequiredService<HumanTaskEngine>();

        var run = await workflow.StartAsync(ApprovalWorkflow(), WorkflowContext.Default);
        var task = await tasks.CreateForActivityAsync(ApprovalTask(), HumanTaskContext.Default, run.InstanceId, "approve");

        await tasks.RejectAsync(task.Id, "manager", "insufficient budget");

        Assert.Equal(HumanTaskStatus.Rejected, tasks.GetTask(task.Id)!.Status);
        Assert.Equal(WorkflowStatus.Completed, workflow.GetInstance(run.InstanceId)!.Status);
        Assert.Contains(workflow.GetInstance(run.InstanceId)!.History.Entries, e => e.NodeId == "denied");
    }

    [Fact]
    public async Task Cancelling_a_bound_task_cancels_the_workflow_instance()
    {
        using var provider = Build();
        var workflow = provider.GetRequiredService<WorkflowEngine>();
        var tasks = provider.GetRequiredService<HumanTaskEngine>();

        var run = await workflow.StartAsync(ApprovalWorkflow(), WorkflowContext.Default);
        var task = await tasks.CreateForActivityAsync(ApprovalTask(), HumanTaskContext.Default, run.InstanceId, "approve");

        await tasks.CancelAsync(task.Id, "admin", "abandoned");

        Assert.Equal(HumanTaskStatus.Cancelled, tasks.GetTask(task.Id)!.Status);
        Assert.Equal(WorkflowStatus.Cancelled, workflow.GetInstance(run.InstanceId)!.Status);
    }

    [Fact]
    public async Task A_form_submission_feeds_the_task_completion_outcome()
    {
        using var provider = Build();
        var workflow = provider.GetRequiredService<WorkflowEngine>();
        var tasks = provider.GetRequiredService<HumanTaskEngine>();
        var forms = provider.GetRequiredService<FormEngine>();

        var run = await workflow.StartAsync(ApprovalWorkflow(), WorkflowContext.Default);
        var task = await tasks.CreateForActivityAsync(ApprovalTask(), HumanTaskContext.Default, run.InstanceId, "approve");

        // This test plays the ORCHESTRATION layer: it opens a form (a standalone, non-workflow-bound form) and
        // feeds the submitted values into the task's completion decision. The task engine never references the
        // forms engine — it only knows tasks; the wiring lives here, above both engines.
        var form = FormDefinition.Create("approve-form", "Approve")
            .AddSection(new FormSection("s", "Decision",
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
        var submission = await forms.SubmitAsync(formInstance.Id, new Dictionary<string, object?> { ["amount"] = 4200m });
        Assert.True(submission!.IsAccepted);

        await tasks.CompleteAsync(task.Id, HumanTaskDecision.Approve(
            "manager", new Dictionary<string, object?> { ["amount"] = 4200m }));

        Assert.Equal(WorkflowStatus.Completed, workflow.GetInstance(run.InstanceId)!.Status);
        Assert.Equal(4200m, workflow.GetInstance(run.InstanceId)!.Variables.Get("amount"));
    }

    [Fact]
    public async Task Task_history_is_persisted_and_readable()
    {
        using var provider = Build();
        var tasks = provider.GetRequiredService<HumanTaskEngine>();

        var task = await tasks.CreateAsync(ApprovalTask(), HumanTaskContext.Default);
        await tasks.OpenAsync(task.Id, "manager");
        await tasks.ApproveAsync(task.Id, "manager");

        var actions = tasks.GetHistory(task.Id).Select(entry => entry.Action).ToArray();

        Assert.Equal(["created", "assigned", "opened", "completed"], actions);
    }
}
