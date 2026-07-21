using FactoryOS.Plugins.Workflow.Engine.Nodes;
using FactoryOS.Plugins.Workflow.Engine.Transitions;

namespace FactoryOS.Plugins.Workflow.Engine.Domain;

/// <summary>
/// An immutable workflow process graph: a keyed, versioned set of nodes and the transitions between them.
/// A definition is validated on build (exactly one start node, at least one end node, and every transition
/// referencing existing nodes) so the executor can trust its shape.
/// </summary>
public sealed class WorkflowDefinition
{
    private readonly Dictionary<string, WorkflowNode> _nodes;
    private readonly List<WorkflowTransition> _transitions;

    internal WorkflowDefinition(
        string key,
        string name,
        WorkflowVersion version,
        Dictionary<string, WorkflowNode> nodes,
        List<WorkflowTransition> transitions,
        string startNodeId)
    {
        Key = key;
        Name = name;
        Version = version;
        _nodes = nodes;
        _transitions = transitions;
        StartNodeId = startNodeId;
    }

    /// <summary>Gets the definition key.</summary>
    public string Key { get; }

    /// <summary>Gets the display name.</summary>
    public string Name { get; }

    /// <summary>Gets the definition version.</summary>
    public WorkflowVersion Version { get; }

    /// <summary>Gets the id of the start node.</summary>
    public string StartNodeId { get; }

    /// <summary>Gets the nodes.</summary>
    public IReadOnlyCollection<WorkflowNode> Nodes => _nodes.Values;

    /// <summary>Gets the transitions.</summary>
    public IReadOnlyList<WorkflowTransition> Transitions => _transitions;

    /// <summary>Gets a node by id.</summary>
    /// <param name="id">The node id.</param>
    /// <returns>The node.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when no node has that id.</exception>
    public WorkflowNode Node(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        return _nodes.TryGetValue(id, out var node)
            ? node
            : throw new KeyNotFoundException($"Definition '{Key}' has no node '{id}'.");
    }

    /// <summary>Gets the transitions leaving a node, in declaration order.</summary>
    /// <param name="nodeId">The source node id.</param>
    /// <returns>The outgoing transitions.</returns>
    public IReadOnlyList<WorkflowTransition> Outgoing(string nodeId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nodeId);
        return _transitions.Where(transition => transition.From == nodeId).ToArray();
    }

    /// <summary>Counts the transitions entering a node (the join arity of a merge).</summary>
    /// <param name="nodeId">The target node id.</param>
    /// <returns>The number of incoming transitions.</returns>
    public int IncomingCount(string nodeId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nodeId);
        return _transitions.Count(transition => transition.To == nodeId);
    }

    /// <summary>Starts a builder for a definition.</summary>
    /// <param name="key">The definition key.</param>
    /// <param name="name">The display name.</param>
    /// <param name="version">The version (defaults to the initial version).</param>
    /// <returns>The builder.</returns>
    public static WorkflowDefinitionBuilder Create(string key, string name, WorkflowVersion version = default) =>
        new(key, name, version == default ? WorkflowVersion.Initial : version);
}

/// <summary>Builds and validates a <see cref="WorkflowDefinition"/>.</summary>
public sealed class WorkflowDefinitionBuilder
{
    private readonly string _key;
    private readonly string _name;
    private readonly WorkflowVersion _version;
    private readonly Dictionary<string, WorkflowNode> _nodes = new(StringComparer.Ordinal);
    private readonly List<WorkflowTransition> _transitions = [];

    internal WorkflowDefinitionBuilder(string key, string name, WorkflowVersion version)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        _key = key;
        _name = name;
        _version = version;
    }

    /// <summary>Adds a node.</summary>
    /// <param name="node">The node to add.</param>
    /// <returns>The same builder, to allow chaining.</returns>
    public WorkflowDefinitionBuilder AddNode(WorkflowNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        if (!_nodes.TryAdd(node.Id, node))
        {
            throw new InvalidOperationException($"Duplicate node id '{node.Id}' in definition '{_key}'.");
        }

        return this;
    }

    /// <summary>Adds a transition between two nodes.</summary>
    /// <param name="from">The source node id.</param>
    /// <param name="to">The target node id.</param>
    /// <param name="condition">The optional guard expression.</param>
    /// <returns>The same builder, to allow chaining.</returns>
    public WorkflowDefinitionBuilder AddTransition(string from, string to, string? condition = null)
    {
        _transitions.Add(new WorkflowTransition(from, to, condition));
        return this;
    }

    /// <summary>Validates and builds the definition.</summary>
    /// <returns>The built definition.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the graph is structurally invalid.</exception>
    public WorkflowDefinition Build()
    {
        var startNodes = _nodes.Values.OfType<StartNode>().ToArray();
        if (startNodes.Length != 1)
        {
            throw new InvalidOperationException(
                $"Definition '{_key}' must have exactly one start node but has {startNodes.Length}.");
        }

        if (!_nodes.Values.OfType<EndNode>().Any())
        {
            throw new InvalidOperationException($"Definition '{_key}' must have at least one end node.");
        }

        foreach (var transition in _transitions)
        {
            if (!_nodes.ContainsKey(transition.From))
            {
                throw new InvalidOperationException(
                    $"Transition in '{_key}' references unknown source node '{transition.From}'.");
            }

            if (!_nodes.ContainsKey(transition.To))
            {
                throw new InvalidOperationException(
                    $"Transition in '{_key}' references unknown target node '{transition.To}'.");
            }
        }

        return new WorkflowDefinition(_key, _name, _version, _nodes, _transitions, startNodes[0].Id);
    }
}
