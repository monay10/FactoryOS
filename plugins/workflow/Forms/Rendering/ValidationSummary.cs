using FactoryOS.Plugins.Forms.Engine.Execution;

namespace FactoryOS.Plugins.Forms.Engine.Rendering;

/// <summary>
/// A display model that aggregates a form's validation errors: the total count, the per-field messages, and a
/// flat list for a summary banner. Built from a <see cref="ValidationResult"/> so a UI can render the outcome
/// without re-running validation.
/// </summary>
public sealed class ValidationSummary
{
    private ValidationSummary(
        bool isValid, IReadOnlyList<string> messages, IReadOnlyDictionary<string, IReadOnlyList<string>> byField)
    {
        IsValid = isValid;
        Messages = messages;
        ByField = byField;
    }

    /// <summary>Gets a value indicating whether the form is valid (no errors).</summary>
    public bool IsValid { get; }

    /// <summary>Gets the total error count.</summary>
    public int Count => Messages.Count;

    /// <summary>Gets every error message in order, for a summary banner.</summary>
    public IReadOnlyList<string> Messages { get; }

    /// <summary>Gets the error messages grouped by field key, for inline display.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> ByField { get; }

    /// <summary>Builds a summary from a validation result.</summary>
    /// <param name="result">The validation result.</param>
    /// <returns>The summary.</returns>
    public static ValidationSummary From(ValidationResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        var messages = result.Errors.Select(error => error.Message).ToArray();
        var byField = result.Errors
            .GroupBy(error => error.FieldKey, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<string>)group.Select(error => error.Message).ToArray(),
                StringComparer.Ordinal);

        return new ValidationSummary(result.IsValid, messages, byField);
    }
}
