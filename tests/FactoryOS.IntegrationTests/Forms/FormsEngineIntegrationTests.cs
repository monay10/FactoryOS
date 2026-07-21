using FactoryOS.Domain.Abstractions;
using FactoryOS.IntegrationTests.Persistence;
using FactoryOS.Plugins.Forms.Engine.Configuration;
using FactoryOS.Plugins.Forms.Engine.Domain;
using FactoryOS.Plugins.Forms.Engine.Execution;
using FactoryOS.Plugins.Workflow.Engine.Domain;
using FactoryOS.Plugins.Workflow.Engine.Execution;
using FactoryOS.Plugins.Workflow.Engine.Nodes;
using Microsoft.Extensions.DependencyInjection;
using WorkflowContext = FactoryOS.Plugins.Workflow.Engine.Configuration.WorkflowContext;

namespace FactoryOS.IntegrationTests.Forms;

/// <summary>
/// The forms engine composed through <c>AddFormsEngine</c> against a real container, driven together with the
/// stateful workflow engine: a workflow pauses on an activity, a form bound to that activity is opened, saved
/// as a draft, blocked while invalid, and — once valid — submitted so the activity completes and the workflow
/// runs to completion. Also covers persistence and container validation.
/// </summary>
public sealed class FormsEngineIntegrationTests
{
    private static readonly DateTimeOffset Now = new(2026, 07, 21, 11, 00, 00, TimeSpan.Zero);

    private static ServiceProvider Build()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IDateTimeProvider>(new FixedClock(Now));
        services.AddFormsEngine();
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
    }

    private static WorkflowDefinition ReviewWorkflow() => WorkflowDefinition.Create("review-wf", "Review Workflow")
        .AddNode(new StartNode("s"))
        .AddNode(new ActivityNode("review", "Review", "review"))
        .AddNode(new EndNode("e"))
        .AddTransition("s", "review")
        .AddTransition("review", "e")
        .Build();

    private static FormDefinition ReviewForm() => FormDefinition.Create("review-form", "Review Form")
        .ForActivity("review")
        .AddSection(new FormSection("s", "Decision",
        [
            new FormGroup("g", null,
            [
                new FormField(new FieldDefinition("approved", "Approved", FieldType.Checkbox)),
                new FormField(new FieldDefinition("note", "Note", FieldType.Text)
                {
                    Validation = new FieldValidation { Required = true, MinLength = 3 },
                }),
            ]),
        ]))
        .Build();

    [Fact]
    public void The_container_composes_the_forms_engine()
    {
        using var provider = Build();

        Assert.NotNull(provider.GetRequiredService<FormEngine>());
        Assert.IsType<WorkflowFormBridge>(provider.GetRequiredService<IFormWorkflowBridge>());
        Assert.NotNull(provider.GetRequiredService<WorkflowEngine>());
    }

    [Fact]
    public async Task A_workflow_activity_opens_a_form_and_a_valid_submission_advances_the_workflow()
    {
        using var provider = Build();
        var workflow = provider.GetRequiredService<WorkflowEngine>();
        var forms = provider.GetRequiredService<FormEngine>();

        // The workflow pauses on its 'review' activity.
        var run = await workflow.StartAsync(ReviewWorkflow(), WorkflowContext.Default);
        Assert.True(run.IsRunning);
        Assert.True(workflow.GetInstance(run.InstanceId)!.PendingActivities.ContainsKey("review"));

        // A form bound to that activity is opened and worked on.
        var form = await forms.OpenForActivityAsync(ReviewForm(), FormContext.Default, run.InstanceId, "review");
        await forms.SaveDraftAsync(form.Id, new Dictionary<string, object?> { ["approved"] = true });

        // An invalid submission is blocked and the workflow stays paused.
        var blocked = await forms.SubmitAsync(form.Id, new Dictionary<string, object?> { ["note"] = "no" });
        Assert.False(blocked!.IsAccepted);
        Assert.True(workflow.GetInstance(run.InstanceId)!.PendingActivities.ContainsKey("review"));

        // A valid submission completes the activity and the workflow runs to the end.
        var accepted = await forms.SubmitAsync(form.Id, new Dictionary<string, object?> { ["note"] = "looks good" });
        Assert.True(accepted!.IsAccepted);
        Assert.Equal(WorkflowStatus.Completed, workflow.GetInstance(run.InstanceId)!.Status);
        Assert.Equal(FormInstanceState.Submitted, forms.GetInstance(form.Id)!.State);
    }

    [Fact]
    public async Task A_submitted_form_is_persisted_and_readable()
    {
        using var provider = Build();
        var forms = provider.GetRequiredService<FormEngine>();

        var form = await forms.OpenAsync(ReviewForm(), FormContext.Default);
        await forms.SaveDraftAsync(form.Id, new Dictionary<string, object?> { ["note"] = "draft text" });
        var reloaded = forms.GetInstance(form.Id);
        Assert.Equal("draft text", reloaded!.Values.Get("note"));

        await forms.SubmitAsync(form.Id, new Dictionary<string, object?> { ["note"] = "final text" }, "auditor");
        var submissions = forms.GetSubmissions(form.Id);

        Assert.Single(submissions);
        Assert.Equal("final text", submissions[0].Values["note"]);
        Assert.Equal("auditor", submissions[0].SubmittedBy);
    }

    [Fact]
    public async Task A_form_bound_to_a_workflow_still_works_when_opened_standalone()
    {
        using var provider = Build();
        var forms = provider.GetRequiredService<FormEngine>();

        // Same definition (ForActivity) but opened without a workflow link: no bridge call happens.
        var form = await forms.OpenAsync(ReviewForm(), FormContext.Default);
        var result = await forms.SubmitAsync(form.Id, new Dictionary<string, object?> { ["note"] = "standalone" });

        Assert.True(result!.IsAccepted);
        Assert.False(forms.GetInstance(form.Id)!.IsWorkflowBound);
    }
}
