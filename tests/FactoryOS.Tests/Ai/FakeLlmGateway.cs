using FactoryOS.Ai.Gateway;
using FactoryOS.Contracts.Ai;
using FactoryOS.Domain.Results;

namespace FactoryOS.Tests.Ai;

/// <summary>
/// A recording in-test <see cref="ILlmGateway"/>. It captures the request it was given and returns a canned
/// answer, so Company Brain orchestration can be verified offline without a real model.
/// </summary>
internal sealed class FakeLlmGateway(string answer = "canned answer") : ILlmGateway
{
    public ChatCompletionRequest? LastRequest { get; private set; }

    public Task<Result<ChatCompletionResponse>> CompleteAsync(ChatCompletionRequest request, CancellationToken cancellationToken)
    {
        LastRequest = request;
        return Task.FromResult(Result.Success(new ChatCompletionResponse
        {
            Model = $"{request.Model}-upstream",
            Content = answer,
        }));
    }

    /// <summary>The concatenated text of the last request's messages, for asserting what the model saw.</summary>
    public string LastPromptText => LastRequest is null
        ? string.Empty
        : string.Join('\n', LastRequest.Messages.Select(m => m.Content));
}
