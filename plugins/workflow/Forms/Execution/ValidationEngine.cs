using System.Collections;
using System.Globalization;
using System.Text.RegularExpressions;
using FactoryOS.Plugins.Forms.Engine.Domain;

namespace FactoryOS.Plugins.Forms.Engine.Execution;

/// <summary>A single validation failure against a field.</summary>
/// <param name="FieldKey">The field that failed.</param>
/// <param name="Message">A human-readable description of the failure.</param>
public sealed record ValidationError(string FieldKey, string Message);

/// <summary>The outcome of validating a form's values: whether it passed and, if not, why.</summary>
/// <param name="Errors">The validation errors, empty when the form is valid.</param>
public sealed record ValidationResult(IReadOnlyList<ValidationError> Errors)
{
    /// <summary>A result with no errors.</summary>
    public static ValidationResult Valid { get; } = new([]);

    /// <summary>Gets a value indicating whether validation passed.</summary>
    public bool IsValid => Errors.Count == 0;
}

/// <summary>
/// Validates a form instance's values against each visible field's constraints — required-ness (static or
/// rule-derived), type, numeric bounds, text length and regular-expression patterns. Fields hidden by a rule
/// are skipped. The engine is pure: it reads values and rules and returns a result without mutating anything.
/// </summary>
public sealed class ValidationEngine
{
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(250);

    private const string EmailPattern = @"^[^@\s]+@[^@\s]+\.[^@\s]+$";
    private const string PhonePattern = @"^\+?[0-9\s\-().]{6,}$";

    private readonly RuleEvaluator _ruleEvaluator;

    /// <summary>Initializes a new instance of the <see cref="ValidationEngine"/> class.</summary>
    /// <param name="ruleEvaluator">The rule evaluator used to resolve visibility and required-ness.</param>
    public ValidationEngine(RuleEvaluator ruleEvaluator)
    {
        ArgumentNullException.ThrowIfNull(ruleEvaluator);
        _ruleEvaluator = ruleEvaluator;
    }

    /// <summary>Validates an instance's current values against its definition.</summary>
    /// <param name="definition">The form definition.</param>
    /// <param name="values">The values to validate.</param>
    /// <returns>The validation result.</returns>
    public ValidationResult Validate(FormDefinition definition, IReadOnlyDictionary<string, object?> values)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(values);

        var evaluation = _ruleEvaluator.Evaluate(definition, values);
        var errors = new List<ValidationError>();

        foreach (var field in definition.Fields.Values)
        {
            if (!field.IsInput)
            {
                continue;
            }

            var visibility = evaluation.For(field.Key);
            if (!visibility.Visible)
            {
                continue;
            }

            var value = values.TryGetValue(field.Key, out var raw) ? raw : null;
            var empty = IsEmpty(value);

            if (visibility.Required && empty)
            {
                errors.Add(new ValidationError(field.Key, $"'{field.Label}' is required."));
                continue;
            }

            if (empty)
            {
                continue;
            }

            ValidateValue(field, value!, errors);
        }

        return new ValidationResult(errors);
    }

    private static void ValidateValue(FieldDefinition field, object value, List<ValidationError> errors)
    {
        var validation = field.Validation;

        switch (field.Type)
        {
            case FieldType.Number:
                ValidateNumber(field, value, wholeOnly: true, errors);
                break;
            case FieldType.Decimal:
            case FieldType.Currency:
                ValidateNumber(field, value, wholeOnly: false, errors);
                break;
            case FieldType.Email:
                ValidatePattern(field, value, EmailPattern, $"'{field.Label}' must be a valid e-mail address.", errors);
                break;
            case FieldType.Phone:
                ValidatePattern(field, value, PhonePattern, $"'{field.Label}' must be a valid phone number.", errors);
                break;
            case FieldType.Date:
            case FieldType.DateTime:
            case FieldType.Time:
                ValidateDate(field, value, errors);
                break;
            default:
                break;
        }

        var text = Stringify(value);
        if (validation.MinLength is int min && text.Length < min)
        {
            errors.Add(new ValidationError(field.Key, $"'{field.Label}' must be at least {min} characters."));
        }

        if (validation.MaxLength is int max && text.Length > max)
        {
            errors.Add(new ValidationError(field.Key, $"'{field.Label}' must be at most {max} characters."));
        }

        if (validation.Pattern is { Length: > 0 } pattern && !IsMatch(pattern, text))
        {
            errors.Add(new ValidationError(field.Key, $"'{field.Label}' has an invalid format."));
        }
    }

    private static void ValidateNumber(FieldDefinition field, object value, bool wholeOnly, List<ValidationError> errors)
    {
        if (!TryToDecimal(value, out var number))
        {
            errors.Add(new ValidationError(field.Key, $"'{field.Label}' must be a number."));
            return;
        }

        if (wholeOnly && decimal.Truncate(number) != number)
        {
            errors.Add(new ValidationError(field.Key, $"'{field.Label}' must be a whole number."));
        }

        if (field.Validation.Min is decimal lower && number < lower)
        {
            errors.Add(new ValidationError(field.Key, $"'{field.Label}' must be at least {lower}."));
        }

        if (field.Validation.Max is decimal upper && number > upper)
        {
            errors.Add(new ValidationError(field.Key, $"'{field.Label}' must be at most {upper}."));
        }
    }

    private static void ValidatePattern(
        FieldDefinition field, object value, string pattern, string message, List<ValidationError> errors)
    {
        if (!IsMatch(pattern, Stringify(value)))
        {
            errors.Add(new ValidationError(field.Key, message));
        }
    }

    private static void ValidateDate(FieldDefinition field, object value, List<ValidationError> errors)
    {
        if (value is DateTime or DateTimeOffset)
        {
            return;
        }

        if (!DateTimeOffset.TryParse(
            Stringify(value), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out _))
        {
            errors.Add(new ValidationError(field.Key, $"'{field.Label}' must be a valid date or time."));
        }
    }

    private static bool IsEmpty(object? value) => value switch
    {
        null => true,
        string text => text.Length == 0,
        IEnumerable enumerable and not string => !enumerable.Cast<object?>().Any(),
        _ => false,
    };

    private static bool TryToDecimal(object value, out decimal result)
    {
        switch (value)
        {
            case decimal d:
                result = d;
                return true;
            case int or long or short or byte or double or float:
                result = Convert.ToDecimal(value, CultureInfo.InvariantCulture);
                return true;
            case string text:
                return decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out result);
            default:
                result = 0m;
                return false;
        }
    }

    private static string Stringify(object value) =>
        Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;

    private static bool IsMatch(string pattern, string text) =>
        Regex.IsMatch(text, pattern, RegexOptions.CultureInvariant, RegexTimeout);
}
