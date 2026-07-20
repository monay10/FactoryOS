using FactoryOS.Contracts.Ai;
using FactoryOS.Domain.Results;

namespace FactoryOS.Ai.Prompts;

/// <summary>The default <see cref="IPromptComposer"/>: catalog lookup + strict render + message assembly.</summary>
public sealed class PromptComposer : IPromptComposer
{
    private readonly IPromptCatalog _catalog;
    private readonly IPromptRenderer _renderer;

    /// <summary>Initializes a new instance of the <see cref="PromptComposer"/> class.</summary>
    /// <param name="catalog">The prompt catalog to resolve templates from.</param>
    /// <param name="renderer">The renderer that substitutes placeholders.</param>
    public PromptComposer(IPromptCatalog catalog, IPromptRenderer renderer)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(renderer);
        _catalog = catalog;
        _renderer = renderer;
    }

    /// <inheritdoc />
    public Result<IReadOnlyList<ChatMessage>> Compose(string key, IReadOnlyDictionary<string, string> variables)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(variables);

        if (!_catalog.TryGet(key, out var template))
        {
            return Result.Failure<IReadOnlyList<ChatMessage>>(Error.NotFound(
                "Ai.Prompt.UnknownTemplate", $"No prompt template is registered for key '{key}'."));
        }

        var messages = new List<ChatMessage>(2);

        if (!string.IsNullOrWhiteSpace(template.System))
        {
            var system = _renderer.Render(template.System, variables);
            if (system.IsFailure)
            {
                return Result.Failure<IReadOnlyList<ChatMessage>>(system.Error);
            }

            messages.Add(new ChatMessage(ChatRole.System, system.Value));
        }

        var user = _renderer.Render(template.User, variables);
        if (user.IsFailure)
        {
            return Result.Failure<IReadOnlyList<ChatMessage>>(user.Error);
        }

        messages.Add(new ChatMessage(ChatRole.User, user.Value));
        return Result.Success<IReadOnlyList<ChatMessage>>(messages);
    }
}
