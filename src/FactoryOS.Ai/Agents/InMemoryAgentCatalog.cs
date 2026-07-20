using System.Collections.Concurrent;
using FactoryOS.Contracts.Ai;

namespace FactoryOS.Ai.Agents;

/// <summary>
/// The default in-memory <see cref="IAgentCatalog"/>. Thread-safe; when the same key is registered more than
/// once it keeps the highest-versioned manifest, mirroring the prompt catalog's versioning rule.
/// </summary>
public sealed class InMemoryAgentCatalog : IAgentCatalog
{
    private readonly ConcurrentDictionary<string, AgentDefinition> _agents = new(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc />
    public void Register(AgentDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        _agents.AddOrUpdate(
            definition.Key,
            definition,
            (_, existing) => definition.Version >= existing.Version ? definition : existing);
    }

    /// <inheritdoc />
    public bool TryGet(string key, out AgentDefinition definition)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return _agents.TryGetValue(key, out definition!);
    }

    /// <inheritdoc />
    public IReadOnlyCollection<AgentDefinition> All => _agents.Values.ToList();
}
