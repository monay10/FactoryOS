using System.Collections.Concurrent;
using FactoryOS.Contracts.Ai;

namespace FactoryOS.Ai.Prompts;

/// <summary>An in-memory <see cref="IPromptCatalog"/>. Keeps the highest registered version per key.</summary>
public sealed class InMemoryPromptCatalog : IPromptCatalog
{
    private readonly ConcurrentDictionary<string, PromptTemplate> _templates = new(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc />
    public void Register(PromptTemplate template)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentException.ThrowIfNullOrWhiteSpace(template.Key);

        _templates.AddOrUpdate(
            template.Key,
            template,
            (_, existing) => template.Version >= existing.Version ? template : existing);
    }

    /// <inheritdoc />
    public bool TryGet(string key, out PromptTemplate template)
    {
        if (key is not null && _templates.TryGetValue(key, out var found))
        {
            template = found;
            return true;
        }

        template = null!;
        return false;
    }
}
