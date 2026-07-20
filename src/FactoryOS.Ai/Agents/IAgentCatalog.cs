using FactoryOS.Contracts.Ai;

namespace FactoryOS.Ai.Agents;

/// <summary>
/// The registry of agent manifests. The runtime discovers agents through this catalog by key — contracts over
/// names — so adding or removing an agent is data, never a runtime change.
/// </summary>
public interface IAgentCatalog
{
    /// <summary>Registers or updates an agent definition (highest version wins).</summary>
    /// <param name="definition">The agent manifest.</param>
    void Register(AgentDefinition definition);

    /// <summary>Resolves an agent by key.</summary>
    /// <param name="key">The agent key.</param>
    /// <param name="definition">The resolved definition, when found.</param>
    /// <returns><see langword="true"/> when an agent with the key is registered.</returns>
    bool TryGet(string key, out AgentDefinition definition);

    /// <summary>All registered agents.</summary>
    IReadOnlyCollection<AgentDefinition> All { get; }
}
