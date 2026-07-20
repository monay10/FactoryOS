using FactoryOS.Domain.Results;

namespace FactoryOS.Ai.Prompts;

/// <summary>Renders a prompt template by substituting its <c>{{variable}}</c> placeholders.</summary>
public interface IPromptRenderer
{
    /// <summary>Renders a template against a set of variables.</summary>
    /// <param name="template">The template text, possibly containing <c>{{name}}</c> placeholders.</param>
    /// <param name="variables">The variable values, keyed by placeholder name.</param>
    /// <returns>A successful result with the rendered text, or a failure listing missing variables.</returns>
    Result<string> Render(string template, IReadOnlyDictionary<string, string> variables);
}
