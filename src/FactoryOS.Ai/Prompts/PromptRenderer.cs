using System.Text;
using System.Text.RegularExpressions;
using FactoryOS.Domain.Results;

namespace FactoryOS.Ai.Prompts;

/// <summary>
/// The default <see cref="IPromptRenderer"/>. Substitution is <b>strict</b>: an unresolved placeholder is an
/// error rather than an empty string, so a malformed prompt never silently reaches a model.
/// </summary>
public sealed partial class PromptRenderer : IPromptRenderer
{
    [GeneratedRegex(@"\{\{\s*([A-Za-z0-9_.]+)\s*\}\}", RegexOptions.CultureInvariant)]
    private static partial Regex PlaceholderRegex();

    /// <inheritdoc />
    public Result<string> Render(string template, IReadOnlyDictionary<string, string> variables)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(variables);

        var missing = new SortedSet<string>(StringComparer.Ordinal);
        var builder = new StringBuilder(template.Length);
        var lastIndex = 0;

        foreach (Match match in PlaceholderRegex().Matches(template))
        {
            var name = match.Groups[1].Value;
            builder.Append(template, lastIndex, match.Index - lastIndex);

            if (variables.TryGetValue(name, out var value))
            {
                builder.Append(value);
            }
            else
            {
                missing.Add(name);
            }

            lastIndex = match.Index + match.Length;
        }

        if (missing.Count > 0)
        {
            return Result.Failure<string>(Error.Validation(
                "Ai.Prompt.MissingVariable",
                $"The template references unbound variable(s): {string.Join(", ", missing)}."));
        }

        builder.Append(template, lastIndex, template.Length - lastIndex);
        return Result.Success(builder.ToString());
    }
}
