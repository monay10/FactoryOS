using System.Text;
using FactoryOS.Plugins.Workflow.Notifications.Domain;

namespace FactoryOS.Plugins.Workflow.Notifications.Execution;

/// <summary>
/// Renders a <see cref="NotificationTemplate"/> against a set of context values by substituting
/// <c>{{token}}</c> placeholders with the matching value. Tokens are trimmed; an unknown token renders as empty
/// text so a missing value never leaks a raw placeholder to the recipient. The rendering is pure and
/// side-effect free.
/// </summary>
public sealed class NotificationTemplateEngine
{
    /// <summary>Renders a template's subject and body against the values.</summary>
    /// <param name="template">The template to render.</param>
    /// <param name="values">The context values.</param>
    /// <returns>The rendered subject and body.</returns>
    public RenderedMessage Render(NotificationTemplate template, IReadOnlyDictionary<string, object?> values)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(values);

        var subject = template.Subject is null ? null : Substitute(template.Subject, values);
        var body = Substitute(template.Body, values);
        return new RenderedMessage(subject, body);
    }

    /// <summary>Substitutes <c>{{token}}</c> placeholders in raw text with the matching values.</summary>
    /// <param name="text">The text to substitute into.</param>
    /// <param name="values">The context values.</param>
    /// <returns>The substituted text.</returns>
    public string Substitute(string text, IReadOnlyDictionary<string, object?> values)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(values);

        if (text.Length == 0 || !text.Contains("{{", StringComparison.Ordinal))
        {
            return text;
        }

        var result = new StringBuilder(text.Length);
        var index = 0;
        while (index < text.Length)
        {
            var open = text.IndexOf("{{", index, StringComparison.Ordinal);
            if (open < 0)
            {
                result.Append(text, index, text.Length - index);
                break;
            }

            var close = text.IndexOf("}}", open + 2, StringComparison.Ordinal);
            if (close < 0)
            {
                result.Append(text, index, text.Length - index);
                break;
            }

            result.Append(text, index, open - index);
            var token = text[(open + 2)..close].Trim();
            if (token.Length > 0 && values.TryGetValue(token, out var value) && value is not null)
            {
                result.Append(value);
            }

            index = close + 2;
        }

        return result.ToString();
    }
}
