using FactoryOS.Contracts.Ai;
using FactoryOS.Domain.Results;

namespace FactoryOS.Ai.Agents;

/// <summary>
/// The shared agent runtime. Every agent — whatever its purpose — runs here; behaviour comes entirely from the
/// resolved <see cref="AgentDefinition"/>. This is the "one runtime, many manifests" rule made concrete.
/// </summary>
public interface IAgentRuntime
{
    /// <summary>Runs the agent named in the request against its input.</summary>
    /// <param name="request">The invocation: tenant, agent key, input and optional prompt variables.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The agent's response, or a failure when the agent is unknown or a step fails.</returns>
    Task<Result<AgentResponse>> RunAsync(AgentRequest request, CancellationToken cancellationToken);
}
