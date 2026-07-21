using FactoryOS.Plugins.Forms.Engine.Configuration;
using FactoryOS.Plugins.Forms.Engine.Diagnostics;
using FactoryOS.Plugins.Forms.Engine.Domain;
using FactoryOS.Plugins.Forms.Engine.Events;
using FactoryOS.Plugins.Forms.Engine.Execution;
using FactoryOS.Plugins.Forms.Engine.Localization;
using FactoryOS.Plugins.Forms.Engine.Persistence;
using FactoryOS.Plugins.Forms.Engine.Rendering;
using FactoryOS.Tests.Identity;

namespace FactoryOS.Tests.Forms;

/// <summary>
/// Unit coverage of the forms engine core: the form builder, the validation and rule engines, the expression
/// language, draft and submission services, permissions, and rendering — exercised directly, without a
/// container. Workflow resumption is proven in the integration suite against a real workflow engine.
/// </summary>
public sealed class FormsEngineCoreTests
{
    private static readonly DateTimeOffset Now = new(2026, 07, 21, 10, 00, 00, TimeSpan.Zero);

    private static FieldDefinition Amount() =>
        new("amount", "Amount", FieldType.Decimal) { Validation = new FieldValidation { Required = true, Min = 0m } };

    private static FieldDefinition Reason() => new("reason", "Reason", FieldType.Text)
    {
        Rules =
        [
            new FieldRule(FieldRuleKind.Required, new FieldCondition("amount > 1000")),
            new FieldRule(FieldRuleKind.Hidden, new FieldCondition("amount <= 1000")),
        ],
    };

    private static FieldDefinition Email() => new("email", "Email", FieldType.Email);

    private static FormDefinition SampleForm(bool withActivity = false)
    {
        var group = new FormGroup(
            "g", "Details", [new FormField(Amount()), new FormField(Reason()), new FormField(Email())]);
        var builder = FormDefinition.Create("expense", "Expense Request")
            .WithTitle("Expense Request")
            .WithLayout(FormLayout.Grid(2))
            .AddSection(new FormSection("s", "Request", [group]));
        if (withActivity)
        {
            builder.ForActivity("review");
        }

        return builder.Build();
    }

    private sealed class Harness
    {
        public Harness(IFormWorkflowBridge? bridge = null)
        {
            var rules = new RuleEvaluator();
            var validation = new ValidationEngine(rules);
            var executor = new FormExecutor(rules, validation);
            Clock = new MutableClock(Now);
            var options = new FormEngineOptions();
            var layout = new LayoutEngine();
            Events = new InMemoryFormEventSink();
            Metrics = new FormMetrics();
            Store = new InMemoryFormStore();
            Repository = new InMemoryFormRepository();
            Submissions = new InMemoryFormSubmissionRepository();
            var versions = new InMemoryFormVersionRepository();
            var localizer = new InMemoryFormLocalizer();
            var renderer = new FormRenderer(rules, layout, localizer);
            var runtime = new FormRuntime(Repository, Store, versions, Events, executor, Metrics, options, Clock);
            var draft = new DraftService(Store, Repository, Events, executor, Metrics, options, Clock);
            var submission = new SubmissionService(
                Store, Repository, Submissions, Events, executor, Metrics, Clock, bridge);
            Engine = new FormEngine(runtime, draft, submission, renderer, Store, Repository, Submissions);
        }

        public FormEngine Engine { get; }

        public MutableClock Clock { get; }

        public InMemoryFormEventSink Events { get; }

        public FormMetrics Metrics { get; }

        public InMemoryFormStore Store { get; }

        public InMemoryFormRepository Repository { get; }

        public InMemoryFormSubmissionRepository Submissions { get; }
    }

    // ---- Form builder ----

    [Fact]
    public void The_builder_indexes_every_field_and_keeps_the_activity_binding()
    {
        var form = SampleForm(withActivity: true);

        Assert.Equal("expense", form.Key);
        Assert.Equal(FormVersion.Initial, form.Version);
        Assert.Equal("review", form.ActivityKey);
        Assert.Equal(3, form.Fields.Count);
        Assert.Equal(FieldType.Decimal, form.Field("amount").Type);
    }

    [Fact]
    public void The_builder_rejects_a_form_with_no_section()
    {
        var builder = FormDefinition.Create("empty", "Empty");
        Assert.Throws<InvalidOperationException>(builder.Build);
    }

    [Fact]
    public void The_builder_rejects_a_duplicate_field_key()
    {
        var group = new FormGroup(
            "g", null, [new FormField(Amount()), new FormField(new FieldDefinition("amount", "Dup", FieldType.Text))]);
        var builder = FormDefinition.Create("dup", "Dup").AddSection(new FormSection("s", "S", [group]));
        Assert.Throws<InvalidOperationException>(builder.Build);
    }

    [Fact]
    public void The_builder_rejects_a_form_with_a_section_but_no_field()
    {
        var builder = FormDefinition.Create("blank", "Blank")
            .AddSection(new FormSection("s", "S", [new FormGroup("g", null, [])]));
        Assert.Throws<InvalidOperationException>(builder.Build);
    }

    // ---- Expression language ----

    [Fact]
    public void A_field_condition_evaluates_arithmetic_and_comparison()
    {
        var condition = new FieldCondition("amount * 2 >= 3000");
        Assert.True(condition.IsSatisfiedBy(new Dictionary<string, object?> { ["amount"] = 1500m }));
        Assert.False(condition.IsSatisfiedBy(new Dictionary<string, object?> { ["amount"] = 1000m }));
    }

    // ---- Conditional rules ----

    [Fact]
    public void A_hidden_rule_removes_a_field_and_a_required_rule_activates_by_condition()
    {
        var form = SampleForm();
        var evaluator = new RuleEvaluator();

        var low = evaluator.Evaluate(form, new Dictionary<string, object?> { ["amount"] = 500m });
        Assert.False(low.For("reason").Visible);
        Assert.False(low.For("reason").Required);

        var high = evaluator.Evaluate(form, new Dictionary<string, object?> { ["amount"] = 2000m });
        Assert.True(high.For("reason").Visible);
        Assert.True(high.For("reason").Required);
    }

    [Fact]
    public void A_calculated_rule_computes_a_field_value()
    {
        var total = new FieldDefinition("total", "Total", FieldType.Decimal)
        {
            Rules = [new FieldRule(FieldRuleKind.Calculated, expression: "quantity * price")],
        };
        var quantity = new FieldDefinition("quantity", "Quantity", FieldType.Number);
        var price = new FieldDefinition("price", "Price", FieldType.Decimal);
        var form = FormDefinition.Create("calc", "Calc")
            .AddSection(new FormSection("s", "S",
                [new FormGroup("g", null, [new FormField(quantity), new FormField(price), new FormField(total)])]))
            .Build();

        var evaluation = new RuleEvaluator().Evaluate(
            form, new Dictionary<string, object?> { ["quantity"] = 3, ["price"] = 10m });

        Assert.Equal(30m, evaluation.Calculated["total"]);
    }

    // ---- Validation ----

    [Fact]
    public void Validation_flags_a_missing_required_field()
    {
        var form = SampleForm();
        var engine = new ValidationEngine(new RuleEvaluator());

        var result = engine.Validate(form, new Dictionary<string, object?> { ["amount"] = null });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.FieldKey == "amount");
    }

    [Fact]
    public void Validation_enforces_numeric_bounds_and_email_format()
    {
        var form = SampleForm();
        var engine = new ValidationEngine(new RuleEvaluator());

        var result = engine.Validate(
            form, new Dictionary<string, object?> { ["amount"] = -5m, ["email"] = "not-an-email" });

        Assert.Contains(result.Errors, error => error.FieldKey == "amount");
        Assert.Contains(result.Errors, error => error.FieldKey == "email");
    }

    [Fact]
    public void Validation_skips_a_field_hidden_by_a_rule()
    {
        var form = SampleForm();
        var engine = new ValidationEngine(new RuleEvaluator());

        // amount <= 1000 hides 'reason', so its conditional Required must not fire.
        var result = engine.Validate(form, new Dictionary<string, object?> { ["amount"] = 500m });

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validation_requires_a_conditionally_required_field_when_visible()
    {
        var form = SampleForm();
        var engine = new ValidationEngine(new RuleEvaluator());

        var result = engine.Validate(form, new Dictionary<string, object?> { ["amount"] = 2000m });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.FieldKey == "reason");
    }

    // ---- Draft ----

    [Fact]
    public async Task Saving_a_draft_persists_values_marks_the_state_and_emits_an_event()
    {
        var harness = new Harness();
        var form = SampleForm();
        var instance = await harness.Engine.OpenAsync(form, FormContext.Default);

        var draft = await harness.Engine.SaveDraftAsync(
            instance.Id, new Dictionary<string, object?> { ["amount"] = 250m });

        Assert.NotNull(draft);
        Assert.Equal(FormInstanceState.Draft, draft!.State);
        Assert.Equal(250m, draft.Values.Get("amount"));
        Assert.Contains(harness.Events.Events, e => e is FormSaved);
    }

    // ---- Submission ----

    [Fact]
    public async Task A_valid_submission_is_accepted_captured_and_announced()
    {
        var harness = new Harness();
        var form = SampleForm();
        var instance = await harness.Engine.OpenAsync(form, FormContext.Default);

        var result = await harness.Engine.SubmitAsync(
            instance.Id, new Dictionary<string, object?> { ["amount"] = 300m }, "u1");

        Assert.NotNull(result);
        Assert.True(result!.IsAccepted);
        Assert.Equal(FormInstanceState.Submitted, harness.Engine.GetInstance(instance.Id)!.State);
        Assert.Single(harness.Engine.GetSubmissions(instance.Id));
        Assert.Contains(harness.Events.Events, e => e is FormSubmitted);
    }

    [Fact]
    public async Task An_invalid_submission_is_blocked_and_leaves_the_instance_editable()
    {
        var harness = new Harness();
        var form = SampleForm();
        var instance = await harness.Engine.OpenAsync(form, FormContext.Default);

        var result = await harness.Engine.SubmitAsync(
            instance.Id, new Dictionary<string, object?> { ["amount"] = 2000m }, "u1");

        Assert.NotNull(result);
        Assert.False(result!.IsAccepted);
        Assert.NotEqual(FormInstanceState.Submitted, harness.Engine.GetInstance(instance.Id)!.State);
        Assert.Empty(harness.Engine.GetSubmissions(instance.Id));
        Assert.DoesNotContain(harness.Events.Events, e => e is FormSubmitted);
    }

    [Fact]
    public async Task A_submitted_instance_can_be_approved()
    {
        var harness = new Harness();
        var instance = await harness.Engine.OpenAsync(SampleForm(), FormContext.Default);
        await harness.Engine.SubmitAsync(instance.Id, new Dictionary<string, object?> { ["amount"] = 100m });

        var approved = await harness.Engine.ApproveAsync(instance.Id);

        Assert.Equal(FormInstanceState.Approved, approved!.State);
    }

    [Fact]
    public async Task An_open_instance_can_be_cancelled()
    {
        var harness = new Harness();
        var instance = await harness.Engine.OpenAsync(SampleForm(), FormContext.Default);

        var cancelled = await harness.Engine.CancelAsync(instance.Id);

        Assert.Equal(FormInstanceState.Cancelled, cancelled!.State);
    }

    // ---- Permissions ----

    [Fact]
    public void A_role_grant_confers_that_access_and_every_lower_one()
    {
        var form = FormDefinition.Create("perm", "Perm")
            .AddPermission(new FormPermission(FormPrincipalKind.Role, "approver", FormAccess.Approve))
            .AddSection(new FormSection("s", "S", [new FormGroup("g", null, [new FormField(Amount())])]))
            .Build();
        var evaluator = new FormPermissionEvaluator();
        var approver = new FormPrincipal("u1", ["approver"], []);
        var other = FormPrincipal.ForUser("u2");

        Assert.True(evaluator.HasAccess(form, approver, FormAccess.Approve));
        Assert.True(evaluator.HasAccess(form, approver, FormAccess.Submit));
        Assert.False(evaluator.HasAccess(form, other, FormAccess.View));
    }

    [Fact]
    public void A_form_with_no_permissions_is_open_to_everyone()
    {
        var form = SampleForm();
        var evaluator = new FormPermissionEvaluator();

        Assert.True(evaluator.HasAccess(form, FormPrincipal.ForUser("anyone"), FormAccess.Submit));
    }

    // ---- Rendering ----

    [Fact]
    public async Task Rendering_drops_hidden_fields_and_lays_visible_ones_onto_the_grid()
    {
        var harness = new Harness();
        var instance = await harness.Engine.OpenAsync(SampleForm(), FormContext.Default);
        await harness.Engine.SaveDraftAsync(instance.Id, new Dictionary<string, object?> { ["amount"] = 500m });

        var rendered = harness.Engine.Render(instance.Id);

        Assert.NotNull(rendered);
        Assert.Equal(FormLayoutKind.Grid, rendered!.Layout.Kind);
        var fieldKeys = rendered.Sections.SelectMany(s => s.Groups).SelectMany(g => g.Fields).Select(f => f.FieldKey);
        Assert.DoesNotContain("reason", fieldKeys); // hidden while amount <= 1000
        Assert.Contains("amount", fieldKeys);
    }

    // ---- Metrics ----

    [Fact]
    public async Task Metrics_count_opens_submissions_and_validation_failures()
    {
        var harness = new Harness();
        var instance = await harness.Engine.OpenAsync(SampleForm(), FormContext.Default);
        await harness.Engine.SubmitAsync(instance.Id, new Dictionary<string, object?> { ["amount"] = 2000m }); // invalid
        await harness.Engine.SubmitAsync(instance.Id, new Dictionary<string, object?> { ["amount"] = 50m });   // valid

        var snapshot = harness.Metrics.Snapshot();

        Assert.Equal(1, snapshot.Opened);
        Assert.Equal(1, snapshot.Submitted);
        Assert.Equal(1, snapshot.ValidationFailures);
    }
}
