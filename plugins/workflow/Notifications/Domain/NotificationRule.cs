using FactoryOS.Plugins.Workflow.Engine.Expressions;

namespace FactoryOS.Plugins.Workflow.Notifications.Domain;

/// <summary>
/// A conditional rule evaluated when a notification is produced. When its condition (an expression over the
/// context values) matches, the rule can suppress the notification entirely, raise or lower its priority, or
/// re-point it at a different template — letting a definition adapt its output to the payload without code.
/// </summary>
public sealed class NotificationRule
{
    private readonly WorkflowExpression _condition;

    /// <summary>Initializes a new instance of the <see cref="NotificationRule"/> class.</summary>
    /// <param name="condition">The condition expression over the context values.</param>
    /// <param name="suppress">Whether a match suppresses the notification.</param>
    /// <param name="priority">The priority a match applies, or <see langword="null"/> to leave it unchanged.</param>
    /// <param name="templateKey">The template key a match selects, or <see langword="null"/> to leave it unchanged.</param>
    public NotificationRule(
        string condition,
        bool suppress = false,
        NotificationPriority? priority = null,
        string? templateKey = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(condition);
        _condition = WorkflowExpression.Parse(condition);
        Condition = condition;
        Suppress = suppress;
        Priority = priority;
        TemplateKey = templateKey;
    }

    /// <summary>Gets the raw condition expression.</summary>
    public string Condition { get; }

    /// <summary>Gets a value indicating whether a match suppresses the notification.</summary>
    public bool Suppress { get; }

    /// <summary>Gets the priority a match applies, if any.</summary>
    public NotificationPriority? Priority { get; }

    /// <summary>Gets the template key a match selects, if any.</summary>
    public string? TemplateKey { get; }

    /// <summary>Evaluates the rule's condition against a set of values.</summary>
    /// <param name="values">The context values.</param>
    /// <returns><see langword="true"/> when the rule matches.</returns>
    public bool Matches(IReadOnlyDictionary<string, object?> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return _condition.EvaluateBoolean(values);
    }
}
