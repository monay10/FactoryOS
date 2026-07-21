using FactoryOS.Plugins.Forms.Engine.Domain;

namespace FactoryOS.Plugins.Forms.Engine.Execution;

/// <summary>The outcome of attempting to submit a form instance.</summary>
/// <param name="Validation">The validation result; drives whether the submission was accepted.</param>
/// <param name="Submission">The captured submission when valid, otherwise <see langword="null"/>.</param>
public sealed record SubmissionResult(ValidationResult Validation, FormSubmission? Submission)
{
    /// <summary>Gets a value indicating whether the submission was accepted.</summary>
    public bool IsAccepted => Validation.IsValid && Submission is not null;
}

/// <summary>
/// The pure state machine of a form instance. It applies field defaults and assignment on open, merges saved
/// values and recomputes calculated fields on draft, and — on submit — validates, and only when validation
/// passes captures a submission snapshot and moves the instance to <see cref="FormInstanceState.Submitted"/>.
/// The executor never persists, publishes events or touches the workflow; the runtime does that around it.
/// </summary>
public sealed class FormExecutor
{
    private readonly RuleEvaluator _ruleEvaluator;
    private readonly ValidationEngine _validationEngine;

    /// <summary>Initializes a new instance of the <see cref="FormExecutor"/> class.</summary>
    /// <param name="ruleEvaluator">The rule evaluator.</param>
    /// <param name="validationEngine">The validation engine.</param>
    public FormExecutor(RuleEvaluator ruleEvaluator, ValidationEngine validationEngine)
    {
        ArgumentNullException.ThrowIfNull(ruleEvaluator);
        ArgumentNullException.ThrowIfNull(validationEngine);
        _ruleEvaluator = ruleEvaluator;
        _validationEngine = validationEngine;
    }

    /// <summary>Applies field defaults, assignment and calculated values to a freshly opened instance.</summary>
    /// <param name="definition">The form definition.</param>
    /// <param name="instance">The instance to prepare.</param>
    /// <param name="openedOnUtc">When the instance opened.</param>
    public void Open(FormDefinition definition, FormInstance instance, DateTimeOffset openedOnUtc)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(instance);

        foreach (var field in definition.Fields.Values)
        {
            if (field.DefaultValue is not null && !instance.Values.Has(field.Key))
            {
                instance.Values.Set(field.Key, field.DefaultValue);
            }
        }

        ApplyCalculations(definition, instance);

        var assignee = definition.Assignment?.Resolve(instance.Values.AsReadOnly());
        instance.AssignTo(assignee);
        instance.History.Append(new FormHistoryEntry(openedOnUtc, "opened", assignee, null));
    }

    /// <summary>Merges saved values into an instance, recomputes calculated fields and marks it a draft.</summary>
    /// <param name="definition">The form definition.</param>
    /// <param name="instance">The instance to update.</param>
    /// <param name="values">The values to merge in.</param>
    /// <param name="savedOnUtc">When the draft was saved.</param>
    /// <param name="trackDraftState">Whether to move an open instance to the draft state.</param>
    public void ApplyDraft(
        FormDefinition definition,
        FormInstance instance,
        IReadOnlyDictionary<string, object?> values,
        DateTimeOffset savedOnUtc,
        bool trackDraftState)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(values);
        EnsureEditable(instance);

        instance.Values.Merge(values);
        ApplyCalculations(definition, instance);
        if (trackDraftState)
        {
            instance.MarkDraft();
        }

        instance.History.Append(new FormHistoryEntry(savedOnUtc, "draft-saved", instance.Assignee, null));
    }

    /// <summary>
    /// Attempts to submit an instance with a final set of values. The values are merged and calculated fields
    /// recomputed, then the result is validated; only on success is a submission captured and the instance
    /// moved to submitted. On failure the instance is left editable and unchanged in state.
    /// </summary>
    /// <param name="definition">The form definition.</param>
    /// <param name="instance">The instance to submit.</param>
    /// <param name="values">The final values to apply before validating.</param>
    /// <param name="submittedBy">Who is submitting.</param>
    /// <param name="submittedOnUtc">When the submission occurs.</param>
    /// <param name="submissionId">The id to assign to a captured submission.</param>
    /// <returns>The submission result.</returns>
    public SubmissionResult TrySubmit(
        FormDefinition definition,
        FormInstance instance,
        IReadOnlyDictionary<string, object?> values,
        string? submittedBy,
        DateTimeOffset submittedOnUtc,
        Guid submissionId)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(values);
        EnsureEditable(instance);

        instance.Values.Merge(values);
        ApplyCalculations(definition, instance);

        var validation = _validationEngine.Validate(definition, instance.Values.AsReadOnly());
        if (!validation.IsValid)
        {
            return new SubmissionResult(validation, null);
        }

        instance.MarkSubmitted(submittedBy);
        instance.History.Append(new FormHistoryEntry(submittedOnUtc, "submitted", submittedBy, null));
        var submission = FormSubmission.Capture(submissionId, instance, submittedOnUtc);
        return new SubmissionResult(validation, submission);
    }

    /// <summary>Resolves the runtime presentation of every field for the instance's current values.</summary>
    /// <param name="definition">The form definition.</param>
    /// <param name="instance">The instance.</param>
    /// <returns>The evaluation.</returns>
    public FormEvaluation Evaluate(FormDefinition definition, FormInstance instance)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(instance);
        return _ruleEvaluator.Evaluate(definition, instance.Values.AsReadOnly());
    }

    private void ApplyCalculations(FormDefinition definition, FormInstance instance)
    {
        var evaluation = _ruleEvaluator.Evaluate(definition, instance.Values.AsReadOnly());
        foreach (var pair in evaluation.Calculated)
        {
            instance.Values.Set(pair.Key, pair.Value);
        }
    }

    private static void EnsureEditable(FormInstance instance)
    {
        if (instance.State is not (FormInstanceState.Open or FormInstanceState.Draft))
        {
            throw new InvalidOperationException(
                $"Form instance '{instance.Id}' is '{instance.State}' and can no longer be edited.");
        }
    }
}
