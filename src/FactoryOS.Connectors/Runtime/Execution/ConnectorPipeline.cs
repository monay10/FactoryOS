using FactoryOS.Connectors.Runtime.Domain;

namespace FactoryOS.Connectors.Runtime.Execution;

/// <summary>
/// Assembles the middleware into the chain every invocation travels, outermost stage first.
/// <para>
/// The order is a design decision, not a preference, and it is fixed by <see cref="IConnectorMiddleware.Order"/>
/// rather than by registration order — because a pipeline whose behaviour depends on which file called
/// <c>Add</c> first is a pipeline that changes meaning when someone tidies the composition root.
/// </para>
/// </summary>
public sealed class ConnectorPipeline
{
    private readonly IReadOnlyList<IConnectorMiddleware> _middleware;

    /// <summary>Initializes a new instance of the <see cref="ConnectorPipeline"/> class.</summary>
    /// <param name="middleware">The stages, in any order; the pipeline sorts them.</param>
    public ConnectorPipeline(IEnumerable<IConnectorMiddleware> middleware)
    {
        ArgumentNullException.ThrowIfNull(middleware);
        _middleware = [.. middleware.OrderBy(stage => stage.Order).ThenBy(stage => stage.Name, StringComparer.Ordinal)];
    }

    /// <summary>Gets the stage names in the order they run, outermost first.</summary>
    /// <returns>The names.</returns>
    public IReadOnlyList<string> Stages() => [.. _middleware.Select(stage => stage.Name)];

    /// <summary>Runs an invocation through every stage and then the terminal handler.</summary>
    /// <param name="invocation">The invocation.</param>
    /// <param name="terminal">What runs once every stage has been entered.</param>
    /// <param name="cancellationToken">A token to cancel the invocation.</param>
    /// <returns>The response.</returns>
    public Task<ConnectorResponse> ExecuteAsync(
        ConnectorInvocation invocation,
        ConnectorInvocationDelegate terminal,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(invocation);
        ArgumentNullException.ThrowIfNull(terminal);

        var next = terminal;
        for (var index = _middleware.Count - 1; index >= 0; index--)
        {
            var stage = _middleware[index];
            var inner = next;
            next = (currentInvocation, token) => stage.InvokeAsync(currentInvocation, inner, token);
        }

        return next(invocation, cancellationToken);
    }
}
